using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
 
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

using Newtonsoft.Json;
using ScrypCommon.Model;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.CognitoIdentityProvider;
using ScrypCommon.Enum;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ScrypPartnerProgram
{
    public class Functions
    {
        // This const is the name of the environment variable that the serverless.template will use to set
        // the name of the DynamoDB table used to store partner posts.
        const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "PartnerTable";

        public const string ID_QUERY_STRING_NAME = "PartnerEmail";
        public const string PAGINATION_TOKEN = "PaginationToken";
        IDynamoDBContext DDBContext { get; set; }

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public Functions()
        {
            // Check to see if a table name was passed in through environment variables and if so 
            // add the table mapping.
            var tableName = System.Environment.GetEnvironmentVariable(TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP);
            if(!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Partner)] = new Amazon.Util.TypeMapping(typeof(Partner), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        /// <summary>
        /// Constructor used for testing passing in a preconfigured DynamoDB client.
        /// </summary>
        /// <param name="ddbClient"></param>
        /// <param name="tableName"></param>
        public Functions(IAmazonDynamoDB ddbClient, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Partner)] = new Amazon.Util.TypeMapping(typeof(Partner), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        /// <summary>
        /// A Lambda function that returns back a page worth of partner posts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of partners</returns>
        public async Task<APIGatewayProxyResponse> GetPartnersAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string paginationToken = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(PAGINATION_TOKEN))
                paginationToken = request.PathParameters[PAGINATION_TOKEN];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(PAGINATION_TOKEN))
                paginationToken = request.QueryStringParameters[PAGINATION_TOKEN];

            context.Logger.LogLine("Getting partners");
            ScanFilter scanFilter = new ScanFilter();
            scanFilter.AddCondition("State", ScanOperator.Equal, 1);
            var search = this.DDBContext.FromScanAsync<Partner>(new Amazon.DynamoDBv2.DocumentModel.ScanOperationConfig
            {
                Filter = scanFilter
            });
            var page = await search.GetNextSetAsync();
            context.Logger.LogLine($"Found {page.Count} partners");
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(page),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };

            return response;
        }

        /// <summary>
        /// A Lambda function that returns the partner identified by partnerEmail
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetPartnerAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string partnerEmail = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                partnerEmail = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                partnerEmail = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(partnerEmail))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting partner {partnerEmail}");
            var partner = await DDBContext.LoadAsync<Partner>(partnerEmail);
            context.Logger.LogLine($"Found partner: {partner != null}");

            if (partner == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound
                };
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(partner),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that adds a partner post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> AddPartnerAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var partner = JsonConvert.DeserializeObject<Partner>(request?.Body);
            var partnerFromDb = await DDBContext.LoadAsync<Partner>(partner.PartnerEmail);
            if (partnerFromDb != null)
            {
                partner.Id = partnerFromDb.Id;
                partner.CreatedTimestamp = partnerFromDb.CreatedTimestamp;
            }
            else
            {
                partner.Id = Guid.NewGuid().ToString();
                partner.CreatedTimestamp = DateTime.Now;
                partner.CreatedBy = request.RequestContext.Authorizer.Claims.GetValueOrDefault("email");
            }

            partner.ModifiedTimestamp = DateTime.Now;
            partner.ModifiedBy = request.RequestContext.Authorizer.Claims.GetValueOrDefault("email");

            context.Logger.LogLine($"Saving partner with id {partner.Id}");
            await DDBContext.SaveAsync<Partner>(partner);

            if (partnerFromDb == null)
            {
                var adminProvider = new AmazonCognitoIdentityProviderClient();
                await adminProvider.AdminCreateUserAsync(new Amazon.CognitoIdentityProvider.Model.AdminCreateUserRequest
                {
                    DesiredDeliveryMediums = { "EMAIL" },
                    ForceAliasCreation = false,
                    TemporaryPassword = Guid.NewGuid().ToString("d").Substring(3, 10),
                    UserAttributes = new List<Amazon.CognitoIdentityProvider.Model.AttributeType>
                {
                    new Amazon.CognitoIdentityProvider.Model.AttributeType
                    {
                        Name = "email",  Value = partner.PartnerEmail
                    },
                    new Amazon.CognitoIdentityProvider.Model.AttributeType
                    {
                        Name = "name",  Value = partner.PartnerName
                    }
                },
                    Username = partner.PartnerEmail,
                    UserPoolId = "us-east-1_ixnBV2gJQ"
                }).ConfigureAwait(false);
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = partner.Id.ToString(),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }
    }
}

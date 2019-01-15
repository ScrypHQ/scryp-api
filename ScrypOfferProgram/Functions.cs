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
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace ScrypOfferProgram
{
    public class Functions
    {
        // This const is the name of the environment variable that the serverless.template will use to set
        // the name of the DynamoDB table used to store offer posts.
        const string TABLENAME_ENVIRONMENT_VARIABLE_LOOKUP = "OfferTable";

        public const string ID_QUERY_STRING_NAME = "Id";
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
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Offer)] = new Amazon.Util.TypeMapping(typeof(Offer), tableName);
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
                AWSConfigsDynamoDB.Context.TypeMappings[typeof(Offer)] = new Amazon.Util.TypeMapping(typeof(Offer), tableName);
            }

            var config = new DynamoDBContextConfig { Conversion = DynamoDBEntryConversion.V2 };
            this.DDBContext = new DynamoDBContext(ddbClient, config);
        }

        /// <summary>
        /// A Lambda function that returns back a page worth of offer posts.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The list of offers</returns>
        public async Task<APIGatewayProxyResponse> GetOffersAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string partnerEmail = request.QueryStringParameters["PartnerEmail"];
            if (string.IsNullOrEmpty(partnerEmail))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest
                };
            }

            //if (request.PathParameters != null && request.PathParameters.ContainsKey(PAGINATION_TOKEN))
            //    paginationToken = request.PathParameters[PAGINATION_TOKEN];
            //else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(PAGINATION_TOKEN))
            //    paginationToken = request.QueryStringParameters[PAGINATION_TOKEN];


            context.Logger.LogLine("Getting offers");
            ScanFilter scanFilter = new ScanFilter();
            scanFilter.AddCondition("IsInactive", ScanOperator.Equal, false);
            scanFilter.AddCondition("OfferedByEmail", ScanOperator.Equal, partnerEmail);
            var search = this.DDBContext.FromScanAsync<Offer>(new Amazon.DynamoDBv2.DocumentModel.ScanOperationConfig
            {
                //Limit = 10,
                //PaginationToken = paginationToken,
                Filter = scanFilter
            });
            var page = await search.GetNextSetAsync();
            context.Logger.LogLine($"Found {page.Count} offers");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(page),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };

            return response;
        }

        /// <summary>
        /// A Lambda function that returns the offer identified by offerId
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> GetOfferAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            string offerId = null;
            if (request.PathParameters != null && request.PathParameters.ContainsKey(ID_QUERY_STRING_NAME))
                offerId = request.PathParameters[ID_QUERY_STRING_NAME];
            else if (request.QueryStringParameters != null && request.QueryStringParameters.ContainsKey(ID_QUERY_STRING_NAME))
                offerId = request.QueryStringParameters[ID_QUERY_STRING_NAME];

            if (string.IsNullOrEmpty(offerId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Body = $"Missing required parameter {ID_QUERY_STRING_NAME}"
                };
            }

            context.Logger.LogLine($"Getting offer {offerId}");
            var offer = await DDBContext.LoadAsync<Offer>(offerId);
            context.Logger.LogLine($"Found offer: {offer != null}");

            if (offer == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.NotFound
                };
            }

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(offer),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }

        /// <summary>
        /// A Lambda function that adds a offer post.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<APIGatewayProxyResponse> AddOfferAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var offer = JsonConvert.DeserializeObject<Offer>(request?.Body);
            Offer offerFromDb = null;
            if (!string.IsNullOrEmpty(offer.Id))
            {
                offerFromDb = await DDBContext.LoadAsync<Offer>(offer.Id);
            }

            if (offerFromDb != null)
            {
                offer.Id = offerFromDb.Id;
                offer.CreatedTimestamp = offerFromDb.CreatedTimestamp;
            }
            else
            {
                offer.Id = Guid.NewGuid().ToString();
                offer.CreatedTimestamp = DateTime.Now;
                offer.CreatedBy = request.RequestContext.Authorizer.Claims.GetValueOrDefault("email");
            }

            offer.ModifiedTimestamp = DateTime.Now;
            offer.ModifiedBy = request.RequestContext.Authorizer.Claims.GetValueOrDefault("email");

            context.Logger.LogLine($"Saving offer with id {offer.Id}");
            await DDBContext.SaveAsync<Offer>(offer);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = offer.Id.ToString(),
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" }, { "Access-Control-Allow-Origin", "*" } }
            };
            return response;
        }
    }
}

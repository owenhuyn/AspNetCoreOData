﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OData.E2E.Tests.Commons;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.OData.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Xunit;

namespace Microsoft.AspNetCore.OData.E2E.Tests.UnboundOperation
{
    public class UnboundOperationTest : WebApiTestBase<UnboundOperationTest>
    {
        public UnboundOperationTest(WebApiTestFixture<UnboundOperationTest> fixture)
            : base(fixture)
        {
        }

#region Set up

        private readonly string EdmSchemaNamespace = typeof(ConventionCustomer).Namespace;

        // following the Fixture convention.
        protected static void UpdateConfigureServices(IServiceCollection services)
        {
            IEdmModel model = UnboundFunctionEdmModel.GetEdmModel();

            services.ConfigureControllers(typeof(ConventionCustomersController), typeof(MetadataController));

            services.AddOData(opt => opt.AddModel("odata", model)
                .Count().Filter().OrderBy().Expand().SetMaxTop(null).Select());
        }

        private async Task<HttpResponseMessage> ResetDatasource()
        {
            var requestUriForPost = "odata/ResetDataSource";
            var requestForPost = new HttpRequestMessage(HttpMethod.Post, requestUriForPost);

            HttpClient client = CreateClient();
            var responseForPost = await client.SendAsync(requestForPost);
            Assert.True(responseForPost.IsSuccessStatusCode);

            return responseForPost;
        }

#endregion

#region Model Builder

        [Fact]
        public async Task MetaDataTest()
        {
            // Arrange
            var requestUri = "odata/$metadata";
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetAsync(requestUri);
            var stream = await response.Content.ReadAsStreamAsync();

            // Assert
            IODataResponseMessage message = new ODataMessageWrapper(stream, response.Content.Headers);
            var reader = new ODataMessageReader(message);
            var edmModel = reader.ReadMetadataDocument();

#region functions
            // Function GetAllConventionCustomers
            var typeOfConventionCustomer = typeof(ConventionCustomer);
            var function1 = edmModel.FindDeclaredOperations(typeOfConventionCustomer.Namespace + ".GetAllConventionCustomers").FirstOrDefault();
            Assert.Equal(string.Format("Collection({0})", typeOfConventionCustomer.FullName), function1.ReturnType.Definition.FullTypeName());
            Assert.Empty(function1.Parameters);

            // Function GetConventionCustomerById
            var function2 = edmModel.FindDeclaredOperations(typeof(ConventionCustomer).Namespace + ".GetConventionCustomerById").FirstOrDefault();
            Assert.Equal(typeOfConventionCustomer.FullName, function2.ReturnType.Definition.FullTypeName());
            Assert.Single(function2.Parameters);

            // Function GetConventionOrderByCustomerIdAndOrderName
            var typeOfConventionOrder = typeof(ConventionOrder);
            var function3 = edmModel.FindDeclaredOperations(typeOfConventionOrder.Namespace + ".GetConventionOrderByCustomerIdAndOrderName").FirstOrDefault();
            Assert.Equal(typeOfConventionOrder.FullName, function3.ReturnType.Definition.FullTypeName());
            Assert.Equal(2, function3.Parameters.Count());
#endregion

#region function imports
            var container = edmModel.EntityContainer;
            Assert.Equal("Container", container.Name);

            var functionImport1 = container.FindOperationImports("GetAllConventionCustomers");
            Assert.Equal(2, functionImport1.Count());

            var functionImport2 = container.FindOperationImports("GetConventionCustomerById");
            Assert.Single(functionImport2);

            var functionImport3 = container.FindOperationImports("GetConventionOrderByCustomerIdAndOrderName");
            Assert.Single(functionImport3);
#endregion

#region actions
            var action2 = edmModel.FindDeclaredOperations(typeOfConventionCustomer.Namespace + ".UpdateAddress").FirstOrDefault();
            Assert.Equal(string.Format("Collection({0})", typeOfConventionCustomer.FullName), action2.ReturnType.Definition.FullTypeName());
            Assert.Equal(2, action2.Parameters.Count());
#endregion

#region action imports
            var actionImport2 = container.FindOperationImports("UpdateAddress");
            Assert.Single(actionImport2);
#endregion
        }

        [Fact]
        public async Task ServiceDocumentTest()
        {
            // Arrange
            var requestUri = "odata";
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetAsync(requestUri);
            var stream = await response.Content.ReadAsStreamAsync();

            //Assert
            var oDataMessageReaderSettings = new ODataMessageReaderSettings();
            IODataResponseMessage message = new ODataMessageWrapper(stream, response.Content.Headers);
            var reader = new ODataMessageReader(message, oDataMessageReaderSettings, UnboundFunctionEdmModel.GetEdmModel());
            var oDataWorkSpace = reader.ReadServiceDocument();

            var function1 = oDataWorkSpace.FunctionImports.Where(odataResourceCollectionInfo => odataResourceCollectionInfo.Name == "GetAllConventionCustomers");
            Assert.Single(function1);
            var function2 = oDataWorkSpace.FunctionImports.Where(odataResourceCollectionInfo => odataResourceCollectionInfo.Name == "GetConventionOrderByCustomerIdAndOrderName");
            // ODL spec says:
            // The edm:FunctionImport for a parameterless function MAY include the IncludeInServiceDocument attribute
            // whose Boolean value indicates whether the function import is advertised in the service document.
            // So the below 2 FunctionImports are not displayed in ServiceDocument.
            Assert.Empty(function2);
            var function3 = oDataWorkSpace.FunctionImports.Where(odataResourceCollectionInfo => odataResourceCollectionInfo.Name == "GetConventionCustomerById");
            Assert.Empty(function3);
        }

#endregion

#region functions and function imports

        [Fact]
        public async Task FunctionImportWithoutParameters()
        {
            // Arrange
            var requestUri = "odata/GetAllConventionCustomersImport()";
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetAsync(requestUri);
            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains("/$metadata#ConventionCustomers", responseString);
            foreach (ConventionCustomer customer in new ConventionCustomersController().Customers)
            {
                string expect = "\"ID\":" + customer.ID;
                Assert.Contains(expect, responseString);
                expect = "\"Name\":\"" + customer.Name + "\"";
                Assert.Contains(expect, responseString);
            }
        }

        [Fact]
        public async Task FunctionImportOverload()
        {
            // Arrange
            var requestUri = "odata/GetAllConventionCustomersImport(CustomerName='Name 1')";
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetAsync(requestUri);
            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains("/$metadata#ConventionCustomers", responseString);
            // only customer 401 and 410 are returned.
            Assert.Contains("\"ID\":401", responseString);
            Assert.Contains("\"ID\":410", responseString);
            Assert.DoesNotContain("\"ID\":402", responseString);
            Assert.DoesNotContain("\"ID\":409", responseString);
        }

        [Theory]
        [InlineData("odata/GetAllConventionCustomersImport(CustomerName='Name 1')/$count", "2")] // returns collection of entity.
        [InlineData("odata/GetDefinedGenders()/$count", "2")] // returns collection of enum
        public async Task DollarCountFollowingFunctionImport(string url, string expectedCount)
        {
            // Arrange & Act
            var requestUri = url;
            HttpClient client = CreateClient();
            var response = await client.GetAsync(requestUri);
            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.NotNull(expectedCount);
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("2", responseString);
        }

        [Fact]
        public async Task FunctionImportWithOneParameters()
        {
            // Arrange
            const int CustomerId = 407;
            ConventionCustomer expectCustomer = new ConventionCustomersController().GetConventionCustomerById(CustomerId);

            // Act
            var requestUri = "odata/GetConventionCustomerByIdImport(CustomerId=" + CustomerId + ")";
            HttpClient client = CreateClient();
            var response = await client.GetAsync(requestUri);
            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.NotNull(expectCustomer);
            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains("/$metadata#ConventionCustomers/$entity", responseString);
            string expect = "\"ID\":" + expectCustomer.ID;
            Assert.Contains(expect, responseString);
            expect = "\"Name\":\"" + expectCustomer.Name + "\"";
            Assert.Contains(expect, responseString);
        }

        [Fact]
        public async Task FunctionImportWithMoreThanOneParameters()
        {
            // Arrange
            const int CustomerId = 408;
            const string OrderName = "OrderName 5";

            var requestUri = "odata/GetConventionOrderByCustomerIdAndOrderNameImport(CustomerId=" + CustomerId + ",OrderName='" + OrderName + "')";
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetWithAcceptAsync(requestUri, "application/json;odata.metadata=full");
            string responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains("/$metadata#ConventionOrders/$entity", responseString);
            Assert.Contains(string.Format("\"@odata.type\":\"#{0}.ConventionOrder", EdmSchemaNamespace), responseString);
            Assert.Contains("\"OrderName\":\"OrderName 5\"", responseString);
            Assert.Contains("\"Price@odata.type\":\"#Decimal\",\"Price\":5", responseString);
        }

        [Fact]
        public async Task FunctionImportFollowedByProperty()
        {
            // Arrange
            const int CustomerId = 407;
            ConventionCustomer expectCustomer = new ConventionCustomersController().GetConventionCustomerById(CustomerId); // expect customer instance
            Assert.NotNull(expectCustomer);

            // Act
            var requestUri = "odata/GetConventionCustomerByIdImport(CustomerId=" + CustomerId + ")/Name";
            HttpClient client = CreateClient();
            var response = await client.GetAsync(requestUri);
            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            string expect = "\"value\":\"" + expectCustomer.Name + "\"";
            Assert.Contains(expect, responseString);
        }

        [Theory]
        [InlineData("GetAllConventionCustomersImport()")]
        [InlineData("GetAllConventionCustomersImport(CustomerName='Name%201')")]
        public async Task FunctionImportFollowedByQueryOption(string functionImport)
        {
            // Arrange
            const int CustomerId = 401;
            ConventionCustomer expectCustomer = (new ConventionCustomersController()).GetConventionCustomerById(CustomerId); // expect customer instance
            Assert.NotNull(expectCustomer);

            // Act
            var requestUri = String.Format("odata/{0}?$filter=ID eq {1}", functionImport, CustomerId);
            HttpClient client = CreateClient();
            var response = await client.GetAsync(requestUri);
            var responseString = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            string expect = "\"Name\":\"" + expectCustomer.Name + "\"";
            Assert.Contains(expect, responseString);
            Assert.DoesNotContain("402", responseString);
        }

        // Negative: Unbound function in query option is not supported
        [Fact]
        public async Task UnboundFunctionInFilter()
        {
            // Arrange
            const int CustomerId = 407;
            ConventionCustomer expectCustomer = new ConventionCustomersController().GetConventionCustomerById(CustomerId); // expect customer instance
            Assert.NotNull(expectCustomer);

            var requestUri = "odata/ConventionCustomers?$filter=Microsoft.AspNetCore.OData.E2E.Tests.UnboundOperation.GetConventionCustomerNameById(CustomerId%3D" + CustomerId + ") eq 'Name 7'";
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetAsync(requestUri);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Re-enable this test after ODL issue is fixed.
        // See ODL issue: https://github.com/OData/odata.net/issues/1155
        // In ODL, e2e test ExceptionSholdThrowForFunctionImport_EnableCaseInsensitive has been added to cover similar
        // scenario on ODL layer.
        //[Fact]
//        public async Task FunctionImportInFilter()
//        {
//            // Arrange
//            const int CustomerId = 407;
//            ConventionCustomer expectCustomer = (new ConventionCustomersController().GetConventionCustomerById(CustomerId)); // expect customer instance
//            Assert.NotNull(expectCustomer);
//
//            // Make sure the function import can be called successfully by following root.
//            var requestFunction = this.BaseAddress + "/odata/GetConventionCustomerNameByIdImport(CustomerId=" + CustomerId + ")";
//            using (var httpResponseMessage = await Client.GetAsync(requestFunction))
//            {
//                string responseString = await httpResponseMessage.Content.ReadAsStringAsync();
//                Assert.Contains("Name 7", responseString);
//            }
//
//            var requestInFilter = this.BaseAddress + "/odata/ConventionCustomers?$filter=GetConventionCustomerNameByIdImport(CustomerId=" + CustomerId + ") eq 'Name 7'";
//            using (var response = await Client.GetAsync(requestInFilter))
//            {
//                Assert.Equal((HttpStatusCode)400, response.StatusCode);
//
//                var json = await response.Content.ReadAsObject<JObject>();
//                var errorMessage = json["error"]["message"].ToString();
//                const string expect = "The query specified in the URI is not valid. An unknown function with name 'GetConventionCustomerNameByIdImport' was found. This may also be a function import or a key lookup on a navigation property, which is not allowed.";
//                Assert.Equal(expect, errorMessage);
//            }
//        }

        [Fact]
        public async Task UnboundFunction_WithPrimitiveEnumComplexEntity_AndCollectionOfThemParameters()
        {
            // Arrange
            var requestUri = string.Format("odata/AdvancedFunction(nums=@a,genders=@b,location=@c,addresses=@d,customer=@e,customers=@f)?@a={0}&@b={1}&@c={2}&@d={3}&@e={4}&@f={5}",
                "[1,2,3]", "['Male','Female']",
                "{\"Street\":\"Zi Xin Rd.\",\"City\":\"Shanghai\",\"ZipCode\":\"2001100\"}",
                "[{\"Street\":\"Zi Xin Rd.\",\"City\":\"Shanghai\",\"ZipCode\":\"2001100\"}]",
                "{\"@odata.type\":\"%23{NAMESPACE}.ConventionCustomer\",\"ID\":7,\"Name\":\"Tony\"}",
                "[{\"@odata.type\":\"%23{NAMESPACE}.ConventionCustomer\",\"ID\":7,\"Name\":\"Tony\"}]"
                );
            requestUri = requestUri.Replace("{NAMESPACE}", EdmSchemaNamespace);
            HttpClient client = CreateClient();

            // Act
            var response = await client.GetAsync(requestUri);

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task UnboundAction_WithPrimitiveEnumComplexEntity_AndCollectionOfThemParameters()
        {
            // Arrange
            var requestUri = "odata/AdvancedAction";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            string payload = @"{
                ""nums"": [4,5,6],
                ""genders"": ['Male', 'Female'],
                ""location"": {""Street"":""NY Rd."",""City"":""Redmond"",""ZipCode"":""9011""},
                ""addresses"": [{""Street"":""NY Rd."",""City"":""Redmond"",""ZipCode"":""9011""}],
                ""customer"": {""@odata.type"":""#{NAMESPACE}.ConventionCustomer"",""ID"":8,""Name"":""Mike""},
                ""customers"": [{""@odata.type"":""#{NAMESPACE}.ConventionCustomer"",""ID"":8,""Name"":""Mike""}]
            }";
            payload = payload.Replace("{NAMESPACE}", EdmSchemaNamespace);
            request.Content = new StringContent(payload);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            request.Content.Headers.ContentLength = payload.Length;
            HttpClient client = CreateClient();

            // Act
            var response = await client.SendAsync(request);

            // Assert
            response.EnsureSuccessStatusCode();
        }

#endregion

#region action imports

        [Fact]
        public async Task ActionImportWithParameters()
        {
            // Arrange
            var uri = "odata/UpdateAddress";
            var content = new { Address = new { Street = "Street 11", City = "City 11", ZipCode = "201101" }, ID = 401 };
            HttpClient client = CreateClient();

            // Act
            var response = await client.PostAsJsonAsync(uri, content);

            // Assert
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            Assert.Contains("Street 11", responseString);
        }

        [Fact]
        public async Task ActionImportFollowedByQueryOption()
        {
            // Arrange
            var uri = "odata/UpdateAddress?$filter=ID%20eq%20402";
            var content = new { Address = new { Street = "Street 11", City = "City 11", ZipCode = "201101" }, ID = 401 };
            HttpClient client = CreateClient();

            // Act
            var response = await client.PostAsJsonAsync(uri, content);

            // Assert
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Street 11", responseString);
        }

#endregion
    }
}
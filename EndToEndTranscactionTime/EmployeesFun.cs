using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos;

namespace EndToEndTranscactionTime
{
    public class EmployeesFun
    {
        private readonly ILogger<EmployeesFun> _logger;
        private readonly Database _myDb;
        private readonly Container _myContainer;
        

        public EmployeesFun(ILogger<EmployeesFun> logger, Tuple<Database, Container> aDbAndContainer)
        {
            _logger = logger;
            _myDb = aDbAndContainer.Item1;
            _myContainer = aDbAndContainer.Item2;
        }

        [FunctionName("InitTest")]
        public async Task<IActionResult> RunInitTest(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                string lEmployeeId = Guid.NewGuid().ToString();
                EmployeeEntity lEmployee = new EmployeeEntity()
                {
                    Id = lEmployeeId,
                    FirstName = $"John",
                    LastName = $"Doe",
                    DateOfBirth = DateTime.Now
                };

                await _myContainer.CreateItemAsync(lEmployee);
                var lResult = await this._myContainer.ReadItemAsync<EmployeeEntity>(lEmployeeId, new PartitionKey(lEmployeeId));                

                return new OkObjectResult("Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not InitTest. Exception thrown: {ex.Message}");

                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }            
        }

        [FunctionName("FinishTest")]
        public async Task<IActionResult> RunFinishTest(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                await _myDb.DeleteAsync();

                return new OkObjectResult("Db deleted ok.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not RunFinishTest. Exception thrown: {ex.Message}");

                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("AddEmployee")]
        public async Task<IActionResult> RunAddEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                string lRrequestBody = await new StreamReader(req.Body).ReadToEndAsync();

                var lEmployee = JsonConvert.DeserializeObject<EmployeeEntity>(lRrequestBody);

                await _myContainer.CreateItemAsync(lEmployee);

                return new OkObjectResult("Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not create new Employee. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetEmployees")]
        public async Task<IActionResult> RunGetEmployees(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                return new OkObjectResult(await GetTopEmployees());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not Get Employees. Exception thrown: {ex.Message}");

                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }            
        }

        private async Task<string> GetTopEmployees()
        {
            List<EmployeeEntity> lResults = new List<EmployeeEntity>();

            QueryDefinition queryDefinition = new QueryDefinition("select top 100 * from Employees");

            using (FeedIterator<EmployeeEntity> feedIterator = this._myContainer.GetItemQueryIterator<EmployeeEntity>(
                queryDefinition,
                null))
            {
                while (feedIterator.HasMoreResults)
                {
                    foreach (var item in await feedIterator.ReadNextAsync())
                    {
                        lResults.Add(item);
                    }
                }                
            }

            return JsonConvert.SerializeObject(lResults);
        }
    }
}

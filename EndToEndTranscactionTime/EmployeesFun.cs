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
        private readonly Container _myDb;

        public EmployeesFun(ILogger<EmployeesFun> logger, Container aDbContainer)
        {
            _logger = logger;
            _myDb = aDbContainer;
        }

        [FunctionName("AddEmployee")]
        public async Task<IActionResult> RunAddEmployee(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            IActionResult lReturnValue = null;

            try
            {
                string lRrequestBody = await new StreamReader(req.Body).ReadToEndAsync();

                var lEmployee = JsonConvert.DeserializeObject<EmployeeEntity>(lRrequestBody);

                await _myDb.CreateItemAsync(lEmployee);

                lReturnValue = new OkObjectResult("Ok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not create new Employee. Exception thrown: {ex.Message}");
                lReturnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return new OkObjectResult(lReturnValue);
        }

        [FunctionName("GetEmployees")]
        public async Task<IActionResult> RunGetEmployees(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            IActionResult returnValue = null;

            try
            {
                List<EmployeeEntity> lResults = new List<EmployeeEntity>();

                QueryDefinition queryDefinition = new QueryDefinition("select top 100 * from Employees");

                using (FeedIterator<EmployeeEntity> feedIterator = this._myDb.GetItemQueryIterator<EmployeeEntity>(
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

                    returnValue = new OkObjectResult(lResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not Get Employees. Exception thrown: {ex.Message}");

                returnValue = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }            

            return new OkObjectResult(returnValue);
        }
    }
}

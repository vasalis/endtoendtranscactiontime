using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleStressTester
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("----- Simple Stress Tester -----");
            Console.WriteLine("Initializing...");

            int lEmployeesToCreate = 50000;

            //string lUseCaseName = "Localhost Test";
            //string lBaseUrl = "http://localhost:7258/api";            

            //string lUseCaseName = "AzFunctions Test";
            //string lBaseUrl = "https://endtoendtranscactiontime.azurewebsites.net/api";

            string lUseCaseName = "Cache - Azure Front Door Test";
            string lBaseUrl = "https://endtoendfunwithcache-d0chdsdab5azd4cc.z01.azurefd.net/api";

            //string lUseCaseName = "NoCache - Azure Front Door Test";
            //string lBaseUrl = "https://endtoendnocache-gghpgzfjd2h4fubu.z01.azurefd.net/api";


            string lUseCaseInitTestUrl = $"{lBaseUrl}/InitTest";
            string lUseCaseFinishUrl = $"{lBaseUrl}/FinishTest";
            string lUseCasePostUrl = $"{lBaseUrl}/AddEmployee";
            string lUseCaseGetUrl = $"{lBaseUrl}/GetEmployees";


            Console.WriteLine($"[Tasks: {GetTasksCount()}] Use case is: {lUseCaseName}, url is: {lBaseUrl}");

            // Create the DI container.
            IServiceCollection services = new ServiceCollection();

            // Being a regular console app, there is no appsettings.json or configuration providers enabled by default.
            // Hence instrumentation key/ connection string and any changes to default logging level must be specified here.
            services.AddLogging(loggingBuilder => loggingBuilder.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("Category", LogLevel.Information));
            services.AddApplicationInsightsTelemetryWorkerService("4c3424aa-520e-495b-a412-baecb74aefce");

            // To pass a connection string
            // - aiserviceoptions must be created
            // - set connectionstring on it
            // - pass it to AddApplicationInsightsTelemetryWorkerService()

            // Build ServiceProvider.
            IServiceProvider serviceProvider = services.BuildServiceProvider();

            // Obtain logger instance from DI.
            ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
            var lTelemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();

            using (var lHttpClient = new HttpClient())
            {
                await InitTest(lHttpClient, lUseCaseInitTestUrl);

                var lEmployees = CreateNewEmployees(lEmployeesToCreate);

                using (var lTel = lTelemetryClient.StartOperation<DependencyTelemetry>($"{lUseCaseName}[{GetTasksCount()}] - POST TEST"))
                {
                    RunPostEmployeesTest(lUseCasePostUrl, lEmployees, lUseCaseName, lTelemetryClient, lHttpClient);
                }

                using (var lTel = lTelemetryClient.StartOperation<DependencyTelemetry>($"{lUseCaseName}[{GetTasksCount()}] - GET TEST"))
                {
                    RunGetEmployeesTest(lUseCaseGetUrl, lEmployeesToCreate, lUseCaseName, lTelemetryClient, lHttpClient);
                }

                await FinishTest(lHttpClient, lUseCaseFinishUrl);
            }
        }

        private static List<StringContent> CreateNewEmployees(int aEmployeesToCreate)
        {
            Console.WriteLine($"Creating {aEmployeesToCreate} new Employee Objects");

            List<StringContent> lEmployees = new List<StringContent>();
            string lDateTimeCreated = DateTime.UtcNow.ToString();            

            for (int i = 0; i < aEmployeesToCreate; i++)
            {
                EmployeeEntity lEmployee = new EmployeeEntity()
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = $"{lDateTimeCreated} first name {i}",
                    LastName = $"{lDateTimeCreated} last name {i}",
                    DateOfBirth = DateTime.Now
                };

                var lJson = JsonConvert.SerializeObject(lEmployee);
                var lData = new StringContent(lJson, Encoding.UTF8, "application/json");

                lEmployees.Add(lData);
            }

            return lEmployees; 
        }

        private static void RunPostEmployeesTest(string aEndPointUrl, List<StringContent> aEmployees, string aUseCaseName, TelemetryClient aTelemetryClient, HttpClient aHttpClient)
        {
            Console.WriteLine($"Sending {aEmployees.Count} new Employee Objects for use case: {aUseCaseName}");
            var lRequestTime = Stopwatch.StartNew();
            PostEmployeesWithTasks(aEndPointUrl, aEmployees, aTelemetryClient, aHttpClient);
            lRequestTime.Stop();
            Console.WriteLine($"POST finished in {lRequestTime.ElapsedMilliseconds / 1000} secs ({lRequestTime.ElapsedMilliseconds} msecs).");
        }

        private static void RunGetEmployeesTest(string aEndPointUrl, int aIterations, string aUseCaseName, TelemetryClient aTelemetryClient, HttpClient aHttpClient)
        {
            Console.WriteLine($"Executing {aIterations} GET(s) for use case: {aUseCaseName}");
            var lRequestTime = Stopwatch.StartNew();
            GetEmployeesAsync(aEndPointUrl, aIterations, aTelemetryClient, aHttpClient);
            lRequestTime.Stop();
            Console.WriteLine($"GET finished in {lRequestTime.ElapsedMilliseconds/1000} secs ({lRequestTime.ElapsedMilliseconds} msecs).");
        }

        private static async Task InitTest(HttpClient aHttpClient, String aUrl)
        {
            Console.WriteLine($"Executing Init on Web side of things...");
            var lResponse = await aHttpClient.GetAsync(aUrl);

            if (!lResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Init failed, status code was: {lResponse.StatusCode}");
                throw new Exception("Test initialization failed...");
            }
            else
            {
                Console.WriteLine($"Init was ok. Response was: {lResponse.Content.ReadAsStringAsync()}");
            }
        }

        private static async Task FinishTest(HttpClient aHttpClient, String aUrl)
        {
            Console.WriteLine($"Finishing test -> Deleting container...");

            Thread.Sleep(5000);

            var lResponse = await aHttpClient.GetAsync(aUrl);

            if (!lResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Finish Test failed, status code was: {lResponse.StatusCode}");                
            }
            else
            {
                var lReply = await lResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"FinishTest was ok. Response was: {lReply}");
            }
        }


        private static async Task PostEmployeesAsync(string aEndPointUrl, List<StringContent> aEmployees, string UseCaseName, TelemetryClient aTelemetryClient, HttpClient aHttpClient)
        {
            foreach (var lEmployee in aEmployees)
            {
                try
                {                    
                    var result = await aHttpClient.PostAsync(aEndPointUrl, lEmployee);                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed: {ex.Message}");
                }
            }
        }

        private static void PostEmployeesWithTasks(string aEndPointUrl, List<StringContent> aEmployees, TelemetryClient aTelemetryClient, HttpClient aHttpClient)
        {
            int lTasksCount = GetTasksCount();
            int lIndex = 0;

            List<StringContent[]> lBigList = new List<StringContent[]>();
            List<StringContent> lSubList = new List<StringContent>();
            foreach (var lEmployee in aEmployees)
            {
                if (lIndex % lTasksCount == 0 && lSubList.Count > 0)
                {
                    lBigList.Add(lSubList.ToArray());
                    lSubList = new List<StringContent>();
                }

                lSubList.Add(lEmployee);
                lIndex++;
            }

            var lTasks = new List<Task>();
            

            foreach(var lSub in lBigList)
            {
                lTasks.Add(Task.Run(async () =>
                {
                    foreach (var lEmployee in lSub)
                    {
                        try
                        {
                            var result = await aHttpClient.PostAsync(aEndPointUrl, lEmployee);
                        }
                        catch (Exception ex)
                        {
                            aTelemetryClient.TrackException(ex);
                            Console.WriteLine($"Post failed: {ex.Message}");
                        }
                    }                   
                }));
            }
            
            try
            {   
                Task.WaitAll(lTasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait failed: {ex.Message}");
            }
        }

        private static void GetEmployeesAsync(string aEndPointUrl, int aIterations, TelemetryClient aTelemetryClient, HttpClient aHttpClient)
        {
            int lTasksCount = GetTasksCount();
            int lIndex = 0;

            List<int[]> lBigList = new List<int[]>();
            List<int> lSubList = new List<int>();
            for(int i = 0; i< aIterations; i++)
            {
                if (lIndex % lTasksCount == 0 && lSubList.Count > 0)
                {
                    lBigList.Add(lSubList.ToArray());
                    lSubList = new List<int>();
                }

                lSubList.Add(i);
                lIndex++;
            }

            var lTasks = new List<Task>();


            foreach (var lSub in lBigList)
            {
                lTasks.Add(Task.Run(async () =>
                {
                    foreach (var lEmployee in lSub)
                    {
                        try
                        {
                            var result = await aHttpClient.GetAsync(aEndPointUrl);
                        }
                        catch (Exception ex)
                        {
                            aTelemetryClient.TrackException(ex);
                            Console.WriteLine($"Get failed: {ex.Message}");
                        }
                    }
                    
                }));
            }

            try
            {
                Task.WaitAll(lTasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wait failed: {ex.Message}");
            }                        
        }

        private static int GetTasksCount()
        {
            return 500;
        }
    }
}

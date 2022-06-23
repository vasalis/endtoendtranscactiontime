using Microsoft.ApplicationInsights;
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
using System.Threading.Tasks;

namespace SimpleStressTester
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("----- Simple Stress Tester -----");
            Console.WriteLine("Initializing...");

            int lEmployeesToCreate = 10000;
            string lDateTimeCreated = DateTime.UtcNow.ToString();
            List<StringContent> lEmployees = new List<StringContent>();

            string lUseCaseName = "Localhost Test";
            string lUseCaseGetUrl = "http://localhost:7258/api/InitTest";
            string lUseCaseFinishUrl = "http://localhost:7258/api/FinishTest";
            string lUseCasePostUrl = "http://localhost:7258/api/AddEmployee";

            //string lUseCaseName = "AzFunctions Test";
            //string lUseCaseGetUrl = "https://endtoendtranscactiontime.azurewebsites.net/api/InitTest";
            //string lUseCasePostUrl = "https://endtoendtranscactiontime.azurewebsites.net/api/AddEmployee";

            //string lUseCaseName = "Azure Front Door Test";
            //string lUseCaseGetUrl = "https://endtoendfun-c2evacc6gge2feea.z01.azurefd.net/api/InitTest";
            //string lUseCasePostUrl = "https://endtoendfun-c2evacc6gge2feea.z01.azurefd.net/api/AddEmployee";

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

            var lHttpClient = new HttpClient();

            await InitTest(lHttpClient, lUseCaseGetUrl);
            
            Console.WriteLine($"Creating {lEmployeesToCreate} new Employee Objects");

            for (int i = 0; i < lEmployeesToCreate; i++)
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

            Console.WriteLine($"Sending {lEmployeesToCreate} new Employee Objects for use case: {lUseCaseName}");            
            
            var lRequestTime = Stopwatch.StartNew();            
            await PostEmployeesAsync(lUseCasePostUrl, lEmployees, lUseCaseName, lTelemetryClient, lHttpClient);
            lRequestTime.Stop();
            Console.WriteLine($"Finished in {lRequestTime.ElapsedMilliseconds} msecs.");

            await FinishTest(lHttpClient, lUseCaseFinishUrl);
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

            var lResponse = await aHttpClient.GetAsync(aUrl);

            if (!lResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Finish Test failed, status code was: {lResponse.StatusCode}");                
            }
            else
            {
                Console.WriteLine($"FinishTest was ok. Response was: {lResponse.Content.ReadAsStringAsync()}");
            }
        }


        private static async Task PostEmployeesAsync(string aEndPointUrl, List<StringContent> aEmployees, string UseCaseName, TelemetryClient aTelemetryClient, HttpClient aHttpClient)
        {
            await Task.Run(() => Parallel.ForEach(aEmployees, async lEmployee =>
            {
                var lRequestTime = Stopwatch.StartNew();
                var result = await aHttpClient.PostAsync(aEndPointUrl, lEmployee);
                lRequestTime.Stop();

                aTelemetryClient.TrackMetric(UseCaseName, lRequestTime.ElapsedMilliseconds);
                Console.WriteLine($"Took: {lRequestTime.ElapsedMilliseconds}");
            }));
        }
    }
}

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
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
            int lEmployeesToCreate = 1000;
            string lDateTimeCreated = DateTime.UtcNow.ToString();
            List<StringContent> lEmployees = new List<StringContent>();

            string lUseCaseName = "Localhost Test";            
            string lUseCasePostUrl = "http://localhost:7258/api/AddEmployee";

            TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();
            configuration.ConnectionString = "InstrumentationKey=0c23ddee-e256-4a16-ac83-097e3df1aecb;IngestionEndpoint=https://northeurope-2.in.applicationinsights.azure.com/;LiveEndpoint=https://northeurope.livediagnostics.monitor.azure.com/";
            var lTelemetryClient = new TelemetryClient(configuration);           


            Console.WriteLine("----- Simple Stress Tester -----");
            Console.WriteLine($"Creating {lEmployeesToCreate} new Employee Objects");

            for (int i = 0; i < lEmployeesToCreate; i++)
            {
                EmployeeEntity lEmployee = new EmployeeEntity()
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = $"{lDateTimeCreated} first name {1}",
                    LastName = $"{lDateTimeCreated} last name {1}",
                    DateOfBirth = DateTime.Now
                };

                var lJson = JsonConvert.SerializeObject(lEmployee);
                var lData = new StringContent(lJson, Encoding.UTF8, "application/json");

                lEmployees.Add(lData);
            }

            Console.WriteLine($"Sending {lEmployeesToCreate} new Employee Objects");            
            
            var lRequestTime = Stopwatch.StartNew();            
            await PostEmployeesAsync(lUseCasePostUrl, lEmployees, lUseCaseName, lTelemetryClient);
            lRequestTime.Stop();
            Console.WriteLine($"Finished in {lRequestTime.ElapsedMilliseconds} msecs.");
        }        

        private static async Task PostEmployeesAsync(string aEndPointUrl, List<StringContent> aEmployees, string UseCaseName, TelemetryClient aTelemetryClient)
        {

            using (var lClient = new HttpClient())
            {
                foreach (var lEmployee in aEmployees)
                {
                    var lRequestTime = Stopwatch.StartNew();
                    var result = await lClient.PostAsync(aEndPointUrl, lEmployee);
                    lRequestTime.Stop();

                    aTelemetryClient.TrackMetric(UseCaseName, lRequestTime.ElapsedMilliseconds);
                }                
            }
        }
    }
}

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: FunctionsStartup(typeof(EndToEndTranscactionTime.Startup))]
namespace EndToEndTranscactionTime
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddFilter(level => true);
            });

            builder.Services.AddSingleton<Tuple<Database, Container>>(GetContainer);
        }

        private Tuple<Database, Container> GetContainer(IServiceProvider options)
        {
            try
            {
                var lConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString");
                var lCosmosDbName = Environment.GetEnvironmentVariable("CosmosDbName");
                var lCosmosDbContainerName = Environment.GetEnvironmentVariable("CosmosDbContainerName");
                var lCosmosDbPartionKey = Environment.GetEnvironmentVariable("CosmosDbPartitionKey");

                var lClient = new CosmosClient(lConnectionString, new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                });

                // Autoscale throughput settings
                // Set autoscale max RU/s
                // WARNING: Be aware of MAX RU!!!
                ThroughputProperties lThroughputProperties = ThroughputProperties.CreateManualThroughput(20000);

                //Create the database with autoscale enabled
                lClient.CreateDatabaseIfNotExistsAsync(lCosmosDbName, throughputProperties: lThroughputProperties).Wait();
                var lDb = lClient.GetDatabase(lCosmosDbName);

                ContainerProperties lAutoscaleContainerProperties = new ContainerProperties(lCosmosDbContainerName, lCosmosDbPartionKey);
                lDb.CreateContainerIfNotExistsAsync(lAutoscaleContainerProperties, lThroughputProperties);

                var lContainer = lDb.GetContainer(lCosmosDbContainerName);

                return new Tuple<Database, Container>(lDb, lContainer);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}

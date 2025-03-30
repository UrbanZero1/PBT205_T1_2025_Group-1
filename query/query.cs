using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

// Checks that the command is used correctly
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: command [hostname] [name]");
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();
    Environment.ExitCode = 1;
    return;
}

string hostname = args[0];
string name = args[1];

var factory = new ConnectionFactory() { HostName = hostname };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync("query", ExchangeType.Topic);

await channel.ExchangeDeclareAsync("query-response", ExchangeType.Topic);

QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
string queueName = queueDeclareResult.QueueName;
await channel.QueueBindAsync(queue: queueName, exchange: "query-response", routingKey: name);

// Creates a consumer for query-response when triggered will output the contacts the person has come into contact with and then exits
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += (model, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    if (message == "!")
    {
        Console.WriteLine($" [x] {name} does not exist in the system");
    }
    else
    {
        Console.WriteLine($" [x] {name} has met {message}");
    }
    Environment.Exit(0);
    return Task.CompletedTask;
};

await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

// Publish a query with the name as the body
await channel.BasicPublishAsync(exchange: "query", routingKey: "query", body: Encoding.UTF8.GetBytes(name));

Console.WriteLine("Press [enter] to exit.");
Console.ReadLine();
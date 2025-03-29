using System;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Tasks;


class Program
{
    static async Task Main()
    {
        Console.Write("Enter your username: "); 
        string username = Console.ReadLine();   // login 

        Console.WriteLine("Enter your messages below. Type 'exit' to quit.");
        var sendTask = SendMessageAsync();
        var receiveTask = ReceiveMessagesAsync();

        await Task.WhenAll(sendTask, receiveTask);
    }

    static async Task SendMessageAsync() // Sending messages
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync
             (queue: "Chat Room",
             durable: false,
             exclusive: false,
             autoDelete: false,
             arguments: null);

        while (true)
        {
            Console.Write("You: ");
            string message = Console.ReadLine();

            if (message.ToLower() == "exit")
                break;

            var body = Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: "Chat Room", body: body);
            Console.WriteLine(" [x] Message sent");
        }
    }
    static async Task ReceiveMessagesAsync() // recieving messages
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync
            (queue: "Chat Room", 
            durable: false, 
            exclusive: false, 
            autoDelete: false,
            arguments: null);

        Console.WriteLine(" [*] Waiting for messages.");

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"\n[x] Received: {message}\nYou: ");
            await Task.CompletedTask;
        };

        await channel.BasicConsumeAsync("Chat Room", autoAck: true, consumer: consumer);

        await Task.Delay(-1);
    }
    
}
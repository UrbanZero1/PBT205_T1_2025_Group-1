using RabbitMQ.Client;
using System.Text;

const int XMAX = 10;
const int YMAX = 10;

// Checks if the command is used correctly
if (args.Length < 3 || args.Length > 4)
{
    Console.Error.WriteLine("Usage: command [hostname] [name] [delay between moves in ms] [(optional)infected: true/false]");
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();
    Environment.ExitCode = 1;
    return;
}

string hostname = args[0];
string name = args[1];
int delay = 1000;

// Checks if the delay is a valid integer
if (int.TryParse(args[2], out delay) == false)
{
    Console.Error.WriteLine("Invalid delay value.");
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();
    Environment.ExitCode = 1;
    return;
}

bool infected = false;

if (args.Length == 4)
{
    if (bool.TryParse(args[3], out infected) == false)
    {
        Console.Error.WriteLine("Invalid infected value.");
        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
        Environment.ExitCode = 1;
        return;
    }
}

// Checks if the delay is greater than 0
if (delay <= 0)
{
    Console.Error.WriteLine("Delay must be greater than 0.");
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();
    Environment.ExitCode = 1;
    return;
}

// Setup RabbitMQ connection
var factory = new ConnectionFactory() { HostName = hostname };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync("position", ExchangeType.Topic);

Random random = new Random();
int xPos = random.Next(XMAX);
int yPos = random.Next(YMAX);

var routingKey = $"new.person.{xPos}.{yPos}.{(infected?"true":"false")}";
var message = name;
var body = Encoding.UTF8.GetBytes(message);

// Publish the message to the exchange
await channel.BasicPublishAsync(exchange: "position", routingKey: routingKey, body: body);

Console.WriteLine($" [x] Sent '{routingKey}':'{message}' to '{hostname}'");
Console.WriteLine();
Console.WriteLine(" Press [enter] to exit.");
var directions = new[] { "up", "down", "left", "right" };
// Set the cursor position for the movement line
int updateLinePosition = Console.CursorTop - 2;

// Loop to send messages every delay milliseconds
var loopTask = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(delay);
        var random = new Random();
        int index = random.Next(directions.Length);
        string direction = directions[index];
        routingKey = "move.person." + direction;
        var loopMessage = name;
        var loopBody = Encoding.UTF8.GetBytes(loopMessage);
        await channel.BasicPublishAsync(exchange: "position", routingKey: routingKey, body: loopBody);
        
        // Update the movement line
        int currentPosition = Console.CursorTop;
        Console.SetCursorPosition(0, updateLinePosition);
        Console.Write(new string(' ', Console.WindowWidth - 1));  // Clear the line
        Console.SetCursorPosition(0, updateLinePosition);
        Console.WriteLine($" [x] Sent move '{direction}'");
        Console.SetCursorPosition(0, currentPosition); // Restore cursor position
    }
});

Console.ReadLine();




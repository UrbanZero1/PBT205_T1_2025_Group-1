using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

// Check if command is used correctly
if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: command [hostname]");
    Console.WriteLine(" Press [enter] to exit.");
    Console.ReadLine();
    Environment.ExitCode = 1;
    return;
}

// Create a new board
Board board = new Board();
// Test data
//board.AddPerson("Alice", 1, 1);
//board.AddPerson("Bob", 2, 2);
//Console.WriteLine(board.AddContact("Alice", "Bob"));

// Create a connection to RabbitMQ
var factory = new ConnectionFactory() { HostName = args[0] };
using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

// Declare the position exchange
await channel.ExchangeDeclareAsync("position", ExchangeType.Topic);

// declare a queue with a random name assigned by the server
QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
string queueName = queueDeclareResult.QueueName;

// the two routing keys are new.person.[x].[y] and move.person.[direction]
string[] routingKeys = ["new.person.*.*", "move.person.*"];

// Bind the queue to the exchange with the routing keys
foreach (var routingKey in routingKeys)
{
    await channel.QueueBindAsync(queue: queueName, exchange: "position", routingKey: routingKey);
}

Console.WriteLine(" [*] Waiting for messages...");

// Create a consumer to handle incoming messages
// The consumer will be triggered when a message is received
// The message body is the name of the person
// The routing key is checked to determine if it's a new person or a move command
// The new person is added to the board or the move command is executed on the given person
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += (modal, ea) =>
{
    var body = ea.Body.ToArray();
    var name = Encoding.UTF8.GetString(body);
    var routingKey = ea.RoutingKey;
    if (routingKey.StartsWith("new.person"))
    {
        var parts = routingKey.Split('.');
        if (parts.Length == 4 && int.TryParse(parts[2], out int xPos) && int.TryParse(parts[3], out int yPos))
        {
            board.AddPerson(name, xPos, yPos);
        }
        else
        {
            Console.WriteLine($"Invalid routing key format for new person: {routingKey}");
        }
    }
    else if (routingKey.StartsWith("move.person"))
    {
        var parts = routingKey.Split('.');
        if (parts.Length == 3)
        {
            string direction = parts[2].ToLower();
            board.MovePerson(name, direction);
        }
    }
    return Task.CompletedTask;
};

await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

// Declare the query and query-response exchanges
await channel.ExchangeDeclareAsync("query", ExchangeType.Topic);
await channel.ExchangeDeclareAsync("query-response", ExchangeType.Topic);

QueueDeclareOk queryDeclareResult = await channel.QueueDeclareAsync();
string queryQueueName = queryDeclareResult.QueueName;

await channel.QueueBindAsync(queue: queryQueueName, exchange: "query", routingKey: "query");

// Create a consumer to handle incoming queries
// When triggered will send a response with the contacts of the person given in the query
// The response is sent to the query-response exchange with the name of the person as the routing key
var queryConsumer = new AsyncEventingBasicConsumer(channel);
queryConsumer.ReceivedAsync += async (modal, ea) =>
{
    var body = ea.Body.ToArray();
    var name = Encoding.UTF8.GetString(body);

    string[] contactNames = board.GetPersonContacts(name);

    if (contactNames != null)
    {
        string response = String.Join(", ", contactNames);
        var responseBody = Encoding.UTF8.GetBytes(response);

        await channel.BasicPublishAsync(
            exchange: "query-response",
            routingKey: name,
            body: responseBody
        );

        Console.WriteLine(" [x] Sent query response.");
    }
    else 
    {
        Console.WriteLine($" [x] {name} does not exist.");
        var responseBody = Encoding.UTF8.GetBytes("!");
        await channel.BasicPublishAsync(
            exchange: "query-response",
            routingKey: name,
            body: responseBody
        );
    }
    await Task.CompletedTask;
};

await channel.BasicConsumeAsync(queryQueueName, autoAck: true, consumer: queryConsumer);

Console.WriteLine("Press [enter] to exit.");
Console.ReadLine();

// Board class holding a grid of Cells and a list of people. The size is by default 10x10 but can be changed in the constructor
class Board
{
    public int width { get; set; }
    public int height { get; set; }

    // Cell class representing a cell in the grid with x and y coordinates
    public struct Cell : IEquatable<Cell>
    {
        public int x { get; set; }
        public int y { get; set; }

        public bool Equals(Cell other)
        {
            return x == other.x && y == other.y;
        }

        public override bool Equals(object? obj)
        {
            return obj is Cell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }
    }

    // Person data structure representing a person with a name, a list of contacts, and the current cell they are in
    public struct Person : IEquatable<Person>
    {
        public string name { get; set; }
        public List<Person> contacts { get; set; }
        public Cell currentCell { get; set; }

        public bool Equals(Person other)
        {
            return name == other.name && contacts.Equals(other.contacts) && currentCell.Equals(other.currentCell);
        }

        public override bool Equals(object? obj)
        {
            return obj is Person other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(name, contacts, currentCell);
        }
    }
    
    // List of people in the board
    public List<Person> people { get; set; }
    
    // 2D array of cells representing the board
    public Cell[,] cells { get; set; }
    
    // Constructor to initialize the board with a given width and height
    public Board(int width = 10, int height = 10)
    {
        this.width = width;
        this.height = height;
        cells = new Cell[width, height];
        people = new List<Person>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y].x = x;
                cells[x, y].y = y;
            }
        }
    }

    // Method to add a person to the board at a given position
    public void AddPerson(string name, int xPos, int yPos)
    {

        foreach (Person person in people)
        {
            if (person.name == name)
            {
                Console.WriteLine($"Person {name} already exists.");
                return;
            }
        }

        Person newPerson = new Person() { name = name, contacts = new List<Person>() };
        

        Random random = new Random();
        if (xPos < 0 || xPos >= width)
        {
            Console.WriteLine($"x is out of range for {name}.");
            return;
        }
        if (yPos < 0 || yPos >= height)
        {
            Console.WriteLine($"y is out of range for {name}.");
            return;
        }
        
        newPerson.currentCell = cells[xPos, yPos];
        
        people.Add(newPerson);
        Console.WriteLine($"Added person {name} at ({xPos}, {yPos}).");
        CheckContacts();
    }

    // Method to move a person in a given direction
    public void MovePerson(string name, string direction)
    {
        int personIndex = people.FindIndex(p => p.name == name);
        
        if (personIndex == -1)
        {
            Console.WriteLine($"Person {name} does not exist.");
            return;
        }

        Person person = people[personIndex];
        Cell currentPos = person.currentCell;
        int newX = currentPos.x;
        int newY = currentPos.y;

        switch (direction)
        {
            case "up":
                newY = Math.Min(height - 1, currentPos.y + 1);
                break;
            case "down":
                newY = Math.Max(0, currentPos.y - 1);
                break;
            case "left":
                newX = Math.Max(0, currentPos.x - 1);
                break;
            case "right":
                newX = Math.Min(width - 1, currentPos.x + 1);
                break;
            default:
                Console.WriteLine($"Invalid direction: {direction}");
                return;
        }

        person.currentCell = cells[newX, newY];
        people[personIndex] = person;
        Console.WriteLine($"Moved {name} {direction} to ({newX}, {newY})");
        CheckContacts();
    }

    // Method to check if two people are in the same cell and add them as contacts
    public void CheckContacts()
    {
        for (int i = 0; i < people.Count; i++)
        {
            Person person = people[i];
            for (int j = 0; j < people.Count; j++)
            {
                Person other = people[j];
                if (person.name != other.name && 
                    person.currentCell.x == other.currentCell.x &&
                    person.currentCell.y == other.currentCell.y)
                {
                    foreach (Person otherPerson in person.contacts)
                    {
                        if (otherPerson.name == other.name)
                        {
                            Console.WriteLine($"{person.name} already has {other.name} as a contact.");
                            return;
                        }
                    }
                    person.contacts.Add(other);
                    Console.WriteLine($"{person.name} has added {other.name} as a contact.");
                }
            }
        }
    }

    // Method to get the contacts of a person in reverse order of when they were added
    public string[] GetPersonContacts(string name)
    {
        int personIndex = people.FindIndex(p => p.name == name);
        if (personIndex == -1)
        {
            Console.WriteLine($"Person {name} does not exist.");
            return null!;
        }
        
        Person person = people[personIndex];
        
        List<string> contacts = person.contacts.Select(c => c.name).ToList();
        contacts.Reverse();
        
        Console.WriteLine($"Query: Found {contacts.Count} contacts for {name}.");
        return contacts.ToArray();
    }

    // USED ONLY IN TESTING. Used to add a contact to a person
    public string AddContact(string name, string contact)
    {
        if (people.FindIndex(p => p.name == name) == -1)
        {
            return $"Person {name} does not exist.";
        }
        if (people.FindIndex(p => p.name == contact) == -1)
        {
            return $"Person {contact} does not exist.";
        }
        int personIndex = people.FindIndex(p => p.name == name);
        int contactIndex = people.FindIndex(p => p.name == contact);
        if (people[personIndex].contacts.Contains(people[contactIndex]))
        {
            return $"{name} already has {contact} as a contact.";
        }
        people[personIndex].contacts.Add(people[contactIndex]);
        return $"{name} added {contact} as a contact.";
    }
}

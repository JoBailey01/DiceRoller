// See https://aka.ms/new-console-template for more information

using SQLitePCL;
using System.Text.Json.Serialization;
using System.Text.Json;


//Load configuration values
LogService.CheckAllConfigs();

Console.WriteLine("Opening DiceLog server...");

//Open a database connexion
var db = new SQLdb("dicelog.db");
db.Open();

//Allow the owner to add new users to the database via command-line arguments
if(args.Length > 0 && args[0].Equals("-n", StringComparison.OrdinalIgnoreCase)){
    Console.WriteLine("Create new user [Y/N]?");
    string? yn = Console.ReadLine();
    {
        if(yn != null && yn.Equals("Y", StringComparison.OrdinalIgnoreCase)){
            Console.WriteLine("Enter new user's username");
            string? username = Console.ReadLine();
            if(username != null){
                Console.WriteLine($"Enter password for new user \'{username}\':");
                string? pass1 = LogService.ReadConsoleBlank();
                if(pass1 == null){
                    Console.WriteLine("Bad console input. Sequence aborted.");
                    goto closeall;    
                }
                Console.WriteLine($"Re-enter password for new user \'{username}\':");
                string? pass2 = LogService.ReadConsoleBlank();
                if(pass2 == null){
                    Console.WriteLine("Bad console input. Sequence aborted.");
                    goto closeall;
                }
                else if(pass2 != pass1){
                    Console.WriteLine("Passwords do not match. Sequence aborted.");
                    goto closeall;
                }

                //Add user to database
                Auth.NewUser(db, username, pass2);

                //Purge passwords from memory
                pass1 = null;
                pass2 = null;
            } else {
                Console.WriteLine("Bad console input. Sequence aborted.");
            }
        }
    }

    goto closeall;
}

//Start the server
Console.WriteLine($"Starting server. IP address:Port = 127.0.0.1:{LogService.configs["ServerPort"]}");
//Create and start a new server using the default port number
var server = new Server(db, -1);
server.RunServer();

closeall:
db.Close();



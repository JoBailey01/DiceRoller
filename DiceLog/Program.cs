/*
    This dummy program establishes a connexion to the server and requests aggregate data for all users. If it is given a command-line argument, it sends that argument as a roll-type request.
*/

Console.WriteLine("Opening DiceLog client (DiceBot)...");

//Create dummy DiscordCommand, possibly using console input
string commText = "3 1d20+6-1d4 Dummy Message"; //Default value
bool sendRoll = false;
if(args.Length > 0){
    commText = "";
    sendRoll = true;
    for(int i = 0;i < args.Length;i++) commText += args[i] + (i < args.Length-1 ? " " : "");
}
var comm1 = new ServerJson.DiscordCommand(5, 0, 1, commText, 5, "dummy-channel", 1, "dummy username", "dummy discriminator", "dummy display name", "2020-10-10T10:10:10Z");

//Statistics command
var comm2 = new ServerJson.DiscordCommand(5, 0, 2, "all;all", 5, "dummy-channel", 1, "dummy username", "dummy discriminator", "dummy display name", "2020-10-10T10:10:10Z");

//Load configuration data
BotService.CheckAllConfigs();

//Create a client using default client and server port numbers
var client = new Client(-1, -1);

//Open a connexion to the server
client.OpenClient();

//Send a dummy Discord command
if(sendRoll) System.Console.WriteLine($"Server response (roll):\n{client.SendCommand(comm1).commandResponse}\n\n");

System.Console.WriteLine($"Server response (statistics):\n{client.SendCommand(comm2).commandResponse}");

client.SendCloseMessage();

//Close the connexion to the server
client.CloseClient();

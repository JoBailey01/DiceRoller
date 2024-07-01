/*
    The BotService class handles configuration data.
*/
class BotService
{
    //A dictionary of all configurations and their current value. They are initialised to their default values here.
    public static Dictionary<string, dynamic> configs = new(){
        //The default IP address of the server
        {"ServerIP", "127.0.0.1"},
        //The default port number to use when connecting to the server
        {"ServerPort", 50023},
        //The default port number for the client
        {"ClientPort", 49223},
        //The default client read timeout, in milliseconds
        {"ClientReadTimeout", 5000},
        //The default client write timeout, in milliseconds
        {"ClientWriteTimeout", 5000},
        //The expected name (CN) of the server
        {"ServerCN", "windows"},
        //The client's username
        {"ClientUsername", "Discord Bot"},
        //The server's thumbprint
        {"ServerThumbprint", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"},
        //The Discord bot's secret token
        {"BotToken", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}
    };

    //A dictionary of the default (initial) values of all configurations
    public static Dictionary<string, dynamic> defaultConfigs = new(configs);

    /*
        Generic helper function for loading configuration values from the .config file
        Parameters: configs is an array of setting names, such as "TokenTimeout"
        Output: an array of booleans, in the same order as the input strings, specifying whether or not any given configuration was found in the file.
        If any valid configuration is specified in configs but not found in .config, then that configuration is reset to its default value.
    */
    public static bool[] CheckConfigs(string[] configNames){
        //Have we updated the configuration values?
        bool[] updated = new bool[configNames.Length];

        //Open the configuration file
        StreamReader sr = File.OpenText(@"./.config");

        //Read the whole file, discarding badly formatted lines
        while(true){
            string? line = sr.ReadLine();
            if(line == null) break;

            //If line is not null, then it should be a real string. Read it. Format: `Name:Value`.
            string[] parts = line.Split(':');

            //Only read lines in the correct format
            if(parts.Length != 2) continue;

            //Convert parts[1] to an int if applicable, or continue on failure
            int val = -1;
            if (BotService.configs[parts[0]].GetType() == val.GetType() &&
                !int.TryParse(parts[1], out val)) continue;

            //Check the input for this configuration value. Ignore config values that are not specified in the input string.
            for(int i = 0;i < configNames.Length;i++){
                if(parts[0] == configNames[i]){
                    updated[i] = true;

                    //Update integer values
                    if(BotService.configs[parts[0]].GetType() == val.GetType()) BotService.configs[parts[0]] = val;
                    //Update string values
                    else BotService.configs[parts[0]] = parts[1];
                }
            }
        }
        sr.Close();

        //If we haven't updated a configuration value, set it to its default value
        for(int i = 0;i < configNames.Length;i++){
            if(!updated[i]) BotService.configs[configNames[i]] = BotService.defaultConfigs[configNames[i]];
        }

        return updated;
    }

    /*
        Runs CheckConfigs on all configurations
    */
    public static void CheckAllConfigs(){
        //Get names of all configs
        string[] configNames = new string[BotService.configs.Count];

        int i = 0;
        foreach(KeyValuePair<string, dynamic> pair in BotService.configs) configNames[i++] = pair.Key;

        CheckConfigs(configNames);
    }

    /*
        Safely read a password from the console without displaying any characters as they are typed
        This code comes from here: https://stackoverflow.com/questions/23433980/c-sharp-console-hide-the-input-from-console-window-while-typing.
    */
    public static string ReadConsoleBlank(){
        string output = "";
        while(true){
            var key = System.Console.ReadKey(true);
            if(key.Key == ConsoleKey.Enter) break;
            output += key.KeyChar;
        }
        return output;
    }
}
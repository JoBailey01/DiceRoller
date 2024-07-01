using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;

/*
    The LogService class handles configuration data, logging dice rolls, and generating aggregate data from dice rolls
*/

class LogService
{
    //A dictionary of all configurations and their current value. They are initialised to their default values here.
    public static Dictionary<string, dynamic> configs = new(){
        //The duration, in seconds, for which a session token is valid after it is issued. Default is 172800 (48 hours).
        {"TokenTimeout", 172800},
        //The default port number for new servers
        {"ServerPort", 50023},
        //The default certificate file name
        {"CertificateFile", "cert.pfx"},
        //The certificate's default password
        {"CertificatePassword", "password1234"},
        //The default SQLite database file name
        {"DatabaseFile", "dicelog.db"},
        //The default server read timeout, in milliseconds
        {"ServerReadTimeout", 5000},
        //The default server write timeout, in milliseconds
        {"ServerWriteTimeout", 5000}
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
            if (LogService.configs[parts[0]].GetType() == val.GetType() &&
                !int.TryParse(parts[1], out val)) continue;

            //Check the input for this configuration value. Ignore config values that are not specified in the input string.
            for(int i = 0;i < configNames.Length;i++){
                if(parts[0] == configNames[i]){
                    updated[i] = true;

                    //Update integer values
                    if(LogService.configs[parts[0]].GetType() == val.GetType()) LogService.configs[parts[0]] = val;
                    //Update string values
                    else LogService.configs[parts[0]] = parts[1];
                }
            }
        }
        sr.Close();

        //If we haven't updated a configuration value, set it to its default value
        for(int i = 0;i < configNames.Length;i++){
            if(!updated[i]) LogService.configs[configNames[i]] = LogService.defaultConfigs[configNames[i]];
        }

        return updated;
    }

    /*
        Runs CheckConfigs on all configurations
    */
    public static void CheckAllConfigs(){
        //Get names of all configs
        string[] configNames = new string[LogService.configs.Count];

        int i = 0;
        foreach(KeyValuePair<string, dynamic> pair in LogService.configs) configNames[i++] = pair.Key;

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

    /*
        Convert an array of integers to a bracketed string (e.g., 5,6,5 => [5,6,5]).
    */
    private static string IntString(int[] nums){
        //Base output string
        string output = "[";

        //Add die rolls to the output string
        for(int i = 0;i < nums.Length;i++){
            output += nums[i] + (i < (nums.Length-1) ? "," : "");
        }

        //Close string and return
        output += "]";
        return output;
    }

    //The help string for ParseCommand
    private static readonly string HelpString = "Notation:"+
            "\n\t/r xdy          | Roll xdy. Add the results."+
            "\n\t/r dy           | Alias for 1dy"+
            "\n\t/r xdy+z        | Roll xdy and add z."+
            "\n\t/r xdy-z        | Roll xdy and add -z."+
            "\n\t/r xdy+vdw      | Roll xdy and vdw. Add the results."+
            "\n\t/r xdy <text>   | Roll xdy and display <text> with the response."+
            "\n\t/r w xdy        | Roll xdy. Repeat for a total of w times."+
            "\n\tAny of the above commands can be combined."+
            "\n\t/r h            | Display notation guide."+
            "\n\t/r help         | Alias for /r h";

    /*
        Parse the text of a Discord roll command and store its results in the database.
        This function does not safety-check the interaction_id.
        The input notation for this bot is largely borrowed from the Dice Maiden bot: https://github.com/Humblemonk/DiceMaiden/blob/master/README.md.
        The parser for this bot is entirely original.
        Notation:
            /r xdy          | Roll xdy. Add the results.
            /r dy           | Alias for 1dy
            /r xd           | Alias for xd6
            /r xdy+z        | Roll xdy and add z.
            /r xdy-z        | Roll xdy and add -z.
            /r xdy+vdw      | Roll xdy and vdw. Add the results.
            /r xdy <text>   | Roll xdy and display <text> with the response.
            /r w xdy        | Roll xdy. Repeat for a total of w times.
            Any of the above commands can be combined.
            /r h            | Display notation guide.
            /r help         | Alias for /r h
    */
    public static string ParseRollCommand(string input, ulong interaction_id, SQLdb db){
        //If the input is an empty string, return help string
        if(input.Length == 0) return HelpString;

        //Then, whitespace-split the string, omitting empty array elements
        string[] elems = input.Split([" ", "\t"], StringSplitOptions.RemoveEmptyEntries);

        //Check for h or help (case-insensitive) or for whitespace-only strings
        if(elems[0].Equals("h", StringComparison.OrdinalIgnoreCase) || elems[0].Equals("help", StringComparison.OrdinalIgnoreCase)) return HelpString;

        //At this point, we may have an actual output
        string output = $"Command: \"{input}\" | Roll: ";

        //The actual parsed roll
        string rollData = "";

        //Check for an initial multipler (w xdy)
        uint repeatLines = 1;
        try {
            repeatLines = UInt32.Parse(elems[0]);

            //Remove elems[0] from the input string from so we can parse the rest of the command
            input = input.Substring(elems[0].Length);
            //foreach (var elem in elems) Console.Write(elem + ","); Console.WriteLine();
        } catch (FormatException){
            //Do nothing if an error occurs. That means this initial value is not an integer.
        }

        //If we only got a leading integer, return it verbatim. Roll no dice and make no records.
        if(input.Length < 1){
            //output += $"{repeatLines}\n Result: `{repeatLines}`";
            output += $"{repeatLines}\n`{repeatLines}`";
            return output;
        }

        //A text label from the end of the input (e.g., <text> in `4d6 <text>`)
        string label = "";

        //Parse the rest of the command, repeating as needed
        for(int i = 0;i < repeatLines;i++){
            //The current line of output, as a string and as a sum
            string line = "";
            int lineTotal = 0;

            //Parse the rest of the string and add the values therein to the line
            int index = 0;
            while(index < input.Length){
                //Consume whitespace
                while((input[index]==' ' || input[index]=='\t') && ++index < input.Length);

                //If the next character(s) are a + or -, compute the sign of the addition operation
                int sign = 1;
                bool signUpdate = false;
                while(index < input.Length){
                    if(input[index]=='+'){
                        sign *= 1;
                        index++;
                        signUpdate = true;
                    } else if(input[index]=='-'){
                        sign *= -1;
                        index++;
                        signUpdate = true;
                    }
                    else break;
                }

                //If there was no sign update and this is not the start of the line, then the rest of the input is a text label <text>. Process it accordingly.
                if(!signUpdate && line.Length > 0){
                    //label = input.Substring(index, input.Length - index);
                    label = input.Substring(index);
                    break;
                }

                //Now, we have removed whitespace and computed the sign of the next element

                //Find the next term (an integer or die roll)
                string term = "";
                int termVal = 0;
                bool hasD = false;
                while(index < input.Length){
                    //Terms may only consist of 0-9 and 'd', and they cannot contain 'd' more than once
                    if(((input[index]=='d' || input[index]=='D') && !hasD) || (input[index]>='0' && input[index]<='9')){
                        term += input[index];
                        //Prevent multiple 'd's from appearing in the same input string
                        if(input[index] == 'd' || input[index] == 'd') hasD = true;
                        index++;
                    } else break;
                }

                //If the term is empty, then this 'term' is actually a label
                if(term.Length==0){
                    label = input.Substring(index);
                    break;
                }

                //Now, we need to compute the numerical value of the term
                term = term.ToLower(); //Avoid case-matching problems
                //Simple integer
                if(term.IndexOf('d') == -1){
                    termVal = Int32.Parse(term);
                    line += (sign == 1 ? "+" : "-") + term;
                    if(i==0) rollData += (sign == 1 ? "+" : "-") + term;
                //Die roll (xdy). On bad input, default to 1d6 (or xd6).
                } else {
                    //First, get the number before 'd'
                    string xStr = term.Substring(0, term.IndexOf('d'));
                    //If there is no such number, default to 1
                    int x = xStr.Length == 0 ? 1 : Int32.Parse(xStr);

                    //Now, get the number after 'd' (the die size)
                    string yStr = term.Substring(term.IndexOf('d')+1);
                    //If there is no such number, default to 6
                    int y = yStr.Length == 0 ? 6 : Int32.Parse(yStr);
                    if(y < 1) y = 6;

                    if(i==0) rollData += $"{(sign == 1 ? "+" : "-")}{x}d{y}";

                    //Roll xdy
                    int[] results = Roll.RollDice(x, y);

                    //Compute the numerical result
                    termVal = results.Sum();

                    //TODO: Report rolls in database!
                    try {
                        foreach(var result in results){
                            db.DbCommand(@"INSERT INTO rolls VALUES($interaction_id, $die_size, $die_roll);",
                                [("$interaction_id", interaction_id), ("die_size", y), ("$die_roll", result)]);
                        }
                    } catch(SqliteException e){
                        Console.WriteLine($"LogService: rolls not saved. Exception: {e.Message}");
                    }

                    //Append the dice output to the line text
                    line += (sign == 1 ? "+" : "-") + LogService.IntString(results);
                }

                //Add the term value to the line total
                lineTotal += termVal * sign;
            }

            //Remove leading '+' from the rollData, if applicable
            if(i==0 && rollData.Length==0){
                rollData = "[No input]";
            }
            else if(i==0 && rollData[0] == '+'){
                rollData = rollData.Substring(1);
            }

            //Add the roll and label to the output before the first line of results, if applicable
            if(i==0 && label.Length > 0){
                output += $"{(repeatLines==1 ? "" : repeatLines + " ")}{rollData} | Tag: {label}\n";
            } else if(i==0){
                output += $"{(repeatLines==1 ? "" : repeatLines + " ")}{rollData}\n";
            }
            
            //Now, we have the text line and its total fully computed

            //Remove leading '+' from the line, if applicable
            if(line.Length==0){
                line = "[No input]";
            }
            else if(line[0] == '+'){
                line = line.Substring(1);
            }

            //Add the line to the output (if there is anything to add)
            //if(rollData!="[No input]") output += (i != 0 ? "\n" : "") + "Result: `" + line + "` = `" + lineTotal + "`";
            if(rollData!="[No input]") output += (i != 0 ? "\n" : "") + "`" + line + "` = `" + lineTotal + "`";
            else{
                output += "[No result]";
                break;
            }
        }

        return output;

    }

    /*
        Parse the test of a Discord statistic command and return it.
        This function does not write to the database. It only performs read operations.
        Input format: <user_id>;<number of seconds>
    */
    public static string ParseStatCommand(string input, int admin_id, SQLdb db){
        //Return an error value on a bad string
        if(!input.Contains(';')) return "Bad input to ParseStatCommand (missing ';')";

        //Parse out the input string
        string[] inputArray = input.Split(';');

        //User ID and seconds (or 'all', if specified)
        bool allUsers = inputArray[0].Equals("all", StringComparison.OrdinalIgnoreCase);
        ulong user_id = 0;
        bool allTime = inputArray[1].Equals("all", StringComparison.OrdinalIgnoreCase);
        ulong seconds_passed = 0;

        //Convert input to integers to ensure that the input is valid
        try {
            if(!allUsers) user_id = UInt64.Parse(inputArray[0]);
            if(!allTime) seconds_passed = UInt64.Parse(inputArray[1]);
        } catch (Exception e){
            return $"Error in input to statistical report: {e.Message}";
        }

        //Giant nightmare SQL query to get statistics
        string queryString =
        """
        SELECT Stat.die_size, Count, Sum, Average, Lowest, Highest, CASE WHEN Natural1s IS NULL THEN 0 ELSE Natural1s END AS Natural1s, CASE WHEN NaturalMax IS NULL THEN 0 ELSE NaturalMax END AS NaturalMax FROM

        (SELECT die_size, COUNT(die_roll) AS Count, SUM(die_roll) AS Sum, PRINTF("%.2f", AVG(die_roll)) AS Average, MIN(die_roll) AS Lowest, MAX(die_roll) AS Highest
            FROM rolls r INNER JOIN commands c ON r.interaction_id = c.interaction_id
            WHERE (c.user_id=$user_id OR 1==$all_users) AND c.admin_id=$admin_id AND (UNIXEPOCH('now') - UNIXEPOCH(c.date_time) <= $seconds_passed OR 1==$all_time)
            GROUP BY die_size) Stat

        FULL OUTER JOIN

        (SELECT die_size, COUNT(die_roll) AS Natural1s
            FROM rolls r INNER JOIN commands c ON r.interaction_id = c.interaction_id
            WHERE (c.user_id=$user_id OR 1==$all_users) AND c.admin_id=$admin_id AND (UNIXEPOCH('now') - UNIXEPOCH(c.date_time) <= $seconds_passed OR 1==$all_time) AND die_roll==1
            GROUP BY die_size) Nat1

        ON Stat.die_size=Nat1.die_size

        FULL OUTER JOIN

        (SELECT die_size, COUNT(die_roll) AS NaturalMax
            FROM rolls r INNER JOIN commands c ON r.interaction_id = c.interaction_id
            WHERE (c.user_id=$user_id OR 1==$all_users) AND c.admin_id=$admin_id AND (UNIXEPOCH('now') - UNIXEPOCH(c.date_time) <= $seconds_passed OR 1==$all_time) AND die_roll=die_size
            GROUP BY die_size) NatMax

        ON Stat.die_size=NatMax.die_size;
        """;

        //Run the SQL query against the database
        var reader = db.DbQuery(queryString, [("$user_id", user_id), ("$admin_id", admin_id), ("$seconds_passed", seconds_passed), ("$all_users", allUsers ? 1 : 0), ("$all_time", allTime ? 1 : 0)]);

        if(!reader.Read()) return "[No data found]";

        //Formatted Output string, complete with headers
        /*
        string output = """
        ```
        Die Size | Count |     Sum | Average | Lowest | Highest | Nat. 1s | Nat. Maxima
        -------- | ----- | ------- | ------- | ------ | ------- | ------- | -----------

        """;
        */
        string output = allUsers ? "Report for all users:" : "Report for requesting user:";
        /*output += """
        
        ```
        Die Size |     Count |       Sum | Average | Nat. 1s | Nat. Maxima
        -------- | --------- | --------- | ------- | ------- | -----------

        """;*/
        output += """
        
        ```
        Die Size |     Count | Average | Nat. 1s | Nat. Maxima
        -------- | --------- | ------- | ------- | -----------

        """;

        //Now, read in each line and produce the output string
        while(true){
            //Add this line to the output table, left-padding and right-aligning all entries
            //output += String.Format("{0,8} | {1,5} | {2,7} | {3,7} | {4,6} | {5,7} | {6,7} | {7,11} \n",
            //    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7));
            output += String.Format("{0,8} | {1,9} | {3,7} | {6,7} | {7,11} \n",
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7));

            if(!reader.Read()) break;
        }

        //Close the reader
        reader.Dispose();
        
        //Close the output string
        output += "```";

        return output;
    }
}

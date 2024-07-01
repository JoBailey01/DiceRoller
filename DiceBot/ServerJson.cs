using System.Text.Json;
using System.Text.Json.Serialization;

/*
    This class stores JSON-related subclasses for serialisation and deserialisation.
    This class only handles messages to and from the server.
*/
class ServerJson
{
    /*
        A message from the server in response to an AuthMessage.
        This message tells the client whether or not the login succeeded. It also contains a session token, which is an empty string ("") for a failed login.
    */
    public class AuthResponse
    {
        //Is the deserialised message a valid AuthResponse?
        [JsonIgnore]
        public bool isCorrect;

        //Did the login succeed?
        //If this is false, then the username and/or password was wrong.
        [JsonInclude]
        public bool? isAuthenticated;

        //Session token ("" for a failed login)
        [JsonInclude]
        public string? token;

        /*
            Constructor to create an AuthResponse by deserialising JSON from the server
        */
        public AuthResponse(string json){
            //var mess = JsonSerializer.Deserialize<AuthResponse>(json);
            AuthResponse? mess;
            try {
                mess = JsonSerializer.Deserialize<AuthResponse>(json);
            } catch (System.Text.Json.JsonException e){
                Console.WriteLine($"Exception in AuthResponse parsing: {e.Message}");
                mess = null;
            }

            if(mess == null){
                this.isAuthenticated = null;
                this.token = null;
                this.isCorrect = false;
                return;
            }
            this.isAuthenticated = mess.isAuthenticated;
            this.token = mess.token;
            this.isCorrect = true;
        }

        /*
            Private JSON constructor
        */
        [JsonConstructor]
        private AuthResponse(bool? isAuthenticated, string? token){
            this.isAuthenticated = isAuthenticated;
            this.token = token;
        }
    }

    /*
        A message from the server in response to a DiscordCommand.
        This message tells the client whether or not the preceding TokenMessage was valid. If the preceding token was not valid, then the client must send an AuthMessage.
        This message also contains the server's response to the previous DiscordCommand, if applicable. If the token was not valid, this value defaults to 'Authentication error (bad session token)'
    */
    public class DiscordResponse
    {
        //Is this message a valid DiscordResponse?
        [JsonIgnore]
        public bool isCorrect;

        //Was the preceding TokenMessage authenticated? (i.e., was the token valid?)
        //If this is false, then the server did not even process the DiscordCommand.
        [JsonInclude]
        public bool? isAuthenticated;

        //The formatted response to the Discord query. This response should be sent to Discord verbatim.
        [JsonInclude]
        public string? commandResponse;

        /*
            JSON deserialisaton constructor
        */
        public DiscordResponse(string json){
            //var mess = JsonSerializer.Deserialize<DiscordResponse>(json);
            DiscordResponse? mess;
            try {
                mess = JsonSerializer.Deserialize<DiscordResponse>(json);
            } catch (System.Text.Json.JsonException e){
                Console.WriteLine($"Exception in DiscordResponse parsing: {e.Message}");
                mess = null;
            }

            if(mess == null){
                this.isAuthenticated = null;
                this.commandResponse = null;
                this.isCorrect = false;
                return;
            }
            this.isAuthenticated = mess.isAuthenticated;
            this.commandResponse = mess.commandResponse;
            this.isCorrect = true;
        }

        /*
            Private JSON constructor
        */
        [JsonConstructor]
        private DiscordResponse(bool? isAuthenticated, string? commandResponse){
            this.isAuthenticated = isAuthenticated;
            this.commandResponse = commandResponse;
        }
    }
    
    /*
        A message from the client that contains specified information from the Discord API.
        The client must send data in this format, not in Discord's raw format.
    */
    public class DiscordCommand
    {
        //Discord interaction_id field
        [JsonInclude]
        private UInt64 interaction_id;

        //Discord interaction type. Dereference this field using DiceJson.DiscordInteractionString() before storing.
        [JsonInclude]
        private int interaction_type;

        //Discord command metadata (application command data). Dereference this field using DiceJson.DiscordCommandName() before storing.
        [JsonInclude]
        private UInt64 command_id;

        //Discord interaction data (message string). In Discord's API, this value is an array of Data Option objects; here, it is formatted based on the command ID.
        [JsonInclude]
        private string interaction_data;

        //Discord channel ID from which this interaction was sent
        [JsonInclude]
        private UInt64 interaction_channel_id;

        //Discord channel name from which this interaction was sent
        [JsonInclude]
        private string interaction_channel_name;

        //Discord user ID of the invoking user, derived from the Discord Guild Member (member?**) object.
        [JsonInclude]
        private UInt64 user_id;

        //Discord username of the invoking user
        [JsonInclude]
        private string username;

        //Discord user discriminator (Discord-tag) of the invoking user
        [JsonInclude]
        private string user_discriminator;

        //Discord user's display name of the invoking user, as of time of invocation
        [JsonInclude]
        private string user_display_name;

        //The date and time of the application command, as determined by the client
        [JsonInclude]
        private string date_time;

        /*
            Public constructor
        */
        public DiscordCommand(UInt64 interaction_id, int interaction_type, UInt64 command_id, string interaction_data, UInt64 interaction_channel_id, string interaction_channel_name, UInt64 user_id, string username, string user_discriminator, string user_display_name, string date_time){
            this.interaction_id = interaction_id;
            this.interaction_type = interaction_type;
            this.command_id = command_id;
            this.interaction_data = interaction_data;
            this.interaction_channel_id = interaction_channel_id;
            this.interaction_channel_name = interaction_channel_name;
            this.user_id = user_id;
            this.username = username;
            this.user_discriminator = user_discriminator;
            this.user_display_name = user_display_name;
            this.date_time = date_time;
        }

        /*
            Generate JSON for this DiscordCommand.
        */
        public string ToJson() => JsonSerializer.Serialize(this);
    }

    /*
        A message from the client that acts as an header file with metadata and a session token.
    */
    public class TokenMessage
    {
        //Session token, stored and transmitted as a string
        [JsonInclude]
        private string token;

        //Is this packet the last in its sequence? If this value is true, then the client plans to send and receive no further packets over this HTTPS session, and the server can close the connexion immediately.
        [JsonInclude]
        private bool isLast;

        /*
            Public constructor
        */
        public TokenMessage(string token, bool isLast){
            this.token = token;
            this.isLast = isLast;
        }

        /*
            Generate JSON for this TokenMessage.
        */
        public string ToJson() => JsonSerializer.Serialize(this);
    }

    /*
        A message from the client that contains authentication data (username and password)    
    */
    public class AuthMessage
    {
        //Username from the client
        [JsonInclude]
        private string username;

        //Password from the client
        [JsonInclude]
        private string password;

        /*
            Public constructor
        */
        public AuthMessage(string username, string password){
            this.username = username;
            this.password = password;
        }

        /*
            Generate JSON for this AuthMessage
        */
        public string ToJson() => JsonSerializer.Serialize(this);
    }
}
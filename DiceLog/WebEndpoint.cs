using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;

/*
    The WebEndpoint class processes client connexions and all logic associated therewith.
    Most of this code comes from here: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream?view=net-8.0.
*/
class WebEndpoint
{
    //The SQLdb object for read/write operations
    private SQLdb db;

    //The certificate for the server
    private X509Certificate serverCert;

    //The client connexion
    private TcpClient client;

    //The SSL stream for this connexion
    private SslStream stream;

    //The admin id of the logged-in user. If -1, the connexion is not logged in at all.
    private int admin_id = -1;

    //The client's IP address and port number
    string clientIP;
    string clientPort;

    //Constructor called from from Server.cs
    public WebEndpoint(SQLdb db, TcpClient client, X509Certificate serverCert, string clientIP, string clientPort){
        this.db = db;
        this.serverCert = serverCert;
        this.client = client;
        this.clientIP = clientIP;
        this.clientPort = clientPort;

        //The second parameter closes the inner client stream so that we can only use the SSL stream
        this.stream = new SslStream(client.GetStream(), false);

        //Authenticate the server but not the client. Allow revoked certificates.
        stream.AuthenticateAsServer(serverCert, clientCertificateRequired: false, checkCertificateRevocation: false);

        //Print authentication data to the console
        Console.WriteLine($"Stream Information\n\tAuthenticated: {stream.IsAuthenticated}\n\tAuthenticated as Server: {stream.IsServer}\n\tProtocol: {stream.SslProtocol}");

        //Default timeouts (subject to temporary change if the user)
        this.stream.ReadTimeout = LogService.configs["ServerReadTimeout"];
        this.stream.WriteTimeout = LogService.configs["ServerWriteTimeout"];
    }

    
    /*
        Read a string from the SSL stream. Only strings up to 4096 bytes can be fully read.
        Most of this code comes from here: https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream.read?view=net-8.0.
    */
    private string ReadStream(){
        //Get UTF-8-encoded bytes
        byte[] buffer = new byte[4096];
        int len = this.stream.Read(buffer, 0, 4096);
        System.Text.Decoder decoder = System.Text.Encoding.UTF8.GetDecoder();

        //Convert bytes to a string
        char[] chars = new char[decoder.GetCharCount(buffer, 0, len)];
        decoder.GetChars(buffer, 0, len, chars, 0);
        string output = new string(chars);

        return output;
    }

    /*
        Write a string to the SSL stream. The message will be cut off if its length exceeds 4096 bytes.
        This function also flushes the stream.
    */
    private void WriteStream(string message){
        //Convert string to UTF-8 encoded bytes
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(message);
        this.stream.Write(bytes, 0, bytes.Length > 4096 ? 4096 : bytes.Length);
        this.stream.Flush();
    }

    /*
        Close the underlying TCP connexion and all associated streams, etc.
    */
    private void CloseStream(){
        this.stream.Dispose();
        this.client.Close();
    }

    /*
        Any break in this flow results in the connexion being closed.

        Flow of traffic from server to client:
        1. Client establishes connexion.
        2. Client sends TokenMessage.
            2a. If TokenMessage.isLast==true, server closes the connexion.
            2b. Server authenticates token.
            2c. Server pings client with meaningless data
        3. Client sends DiscordCommand.
            3a. If 2b returned true, then server processes DiscordCommand.
        4. Server sends DiscordResponse.
            4a. If DiscordResponse.isAuthenticated==false (invalid session token from step 2b):
                4a1. Client sends AuthMessage with username and password.
                    4a1a. If credentials are invalid, server sends AuthResponse with AuthResponse.isAuthenticated=false. Return to step 4a1. Stop after 5 failed attempts.
                    4a1b. If credentials are valid, server sends AuthResponse with AuthResponse.isAuthenticated=true.
                        4a1b1. Client returns to step 2 with the new token and resends both the TokenMessage and the DiscordCommand.
            4b. If DiscordResponse.isAuthenticated==true (valid session token from step 2b):
                4b1. If the token's admin ID does not match the previous admin ID of this stream, then server terminates connexion without sending any message.
                4b2. Client processes DiscordResponse.
        5. Return to step 2.
    */

    /*
        This function handles ongoing traffic from the client once the WebEndpoint has authenticated the connexion as the server.
        This function runs indefinitely until the client closes the connexion or sends a message telling the server to do so. Use CTRL+C to cancel it prematurely.
    */
    public void ProcessTraffic(){
        while(true){
            //2. Wait for client TokenMessage
            string tokenMessageData = this.ReadStream();
            
            //Decode this message, from JSON, as a TokenMessage
            DiceJson.TokenMessage tokenMessage = new(tokenMessageData);

            //2a. If this is a bad message or a client-flagged last message, kill the connexion immediately and return
            if(!tokenMessage.isCorrect || tokenMessage.isLast == null || (bool) tokenMessage.isLast){
                //Console.WriteLine($"Bad TokenMessage received. Closing connexion to {this.clientIP}:{this.clientPort}.");
                //if (!tokenMessage.isCorrect || tokenMessage.isLast == null) Console.WriteLine($"Bad data from client: {tokenMessageData}");
                Console.WriteLine($"Closing connexion to {this.clientIP}:{this.clientPort}{(tokenMessage.isLast != null && (bool) tokenMessage.isLast ? " by client request" : " due to end of stream or bad TokenMessage")}.");
                this.CloseStream();
                return;
            }

            //2b. Now, authenticate the token
            var (token_admin_id, token_validity) = tokenMessage.CheckValidity(this.db);

            //2c. Send a data-less ping to the client
            this.WriteStream("0000");

            //3. Read the DiscordCommand (which we may simply discard)
            string discordCommandData = this.ReadStream();

            //4a. If the token was invalid, handle authentication logic
            if(!token_validity){
                //Send the DiscordResponse with isAuthenticated=false
                DiceJson.DiscordResponse discordResponse = new(false, "");
                this.WriteStream(discordResponse.ToJson());

                //Extend the maximum read time by 60 seconds to allow the user to enter credentials
                this.stream.ReadTimeout = LogService.configs["ServerReadTimeout"] + 60000;

                //The number of times the client has tried and failed to provide credentials
                int failedAuths = 0;

                //Authentication loop
                while(failedAuths <= 6){
                    //If the client has failed 5 authorisation attempts already, stop and close the connexion
                    if(failedAuths>=5){
                        this.stream.ReadTimeout = LogService.configs["ServerReadTimeout"];
                        this.CloseStream();
                        return;
                    }

                    //4a1. Wait for client AuthMessage
                    string authMessageData = this.ReadStream();

                    //Decode this message as an AuthMessage
                    DiceJson.AuthMessage authMessage = new(authMessageData, this.db);

                    //4a1a. If this is a bad packet or the credentials are invalid, respond accordingly
                    if(!authMessage.isCorrect || authMessage.admin_id == -1){
                        DiceJson.AuthResponse authResponse = new(false, "");
                        this.WriteStream(authResponse.ToJson());
                        failedAuths++;
                        continue;

                    //4a1b. If the credentials are valid (authMessage.admin_id != -1), then respond accordingly and update the local admin_id
                    } else {
                        this.admin_id = authMessage.admin_id;
                        DiceJson.AuthResponse authResponse = new(true, Auth.NewToken(db, this.admin_id));
                        this.WriteStream(authResponse.ToJson());
                        this.stream.ReadTimeout = LogService.configs["ServerReadTimeout"]; //Reset read timeout
                        break; //Exit the authentication loop
                    }
                }
            //4b. If the token was valid, send a DiscordResponse
            } else {
                //If the local admin_id has not been set, set stored local admin_id to the authenticated token's admin_id.
                if(this.admin_id == -1) this.admin_id = token_admin_id;
                
                //If the local admin_id has been set but does not match the token, then declare the token invalid and terminate the connexion without authenticating.
                //This case likely indicates an attack.
                else if (this.admin_id != token_admin_id){
                    this.CloseStream();
                    return;
                }

                DiceJson.DiscordResponse discordResponse;

                //Deserialise the JSON from step 3.
                DiceJson.DiscordCommand discordCommand = new(discordCommandData);

                //If this is a bad packet, respond accordingly
                if(!discordCommand.isCorrect || discordCommand.interaction_data==null || discordCommand.interaction_id==null
                    || discordCommand.user_id==null || discordCommand.user_display_name==null){
                    Console.WriteLine("Received malformed DiscordCommand");
                    discordResponse = new(true, "Error: Malformed DiscordCommand Packet");
                } else {
                    
                    Console.WriteLine($"Received valid DiscordCommand (type {discordCommand.command_id}): {discordCommand.interaction_data}.");

                    //Save the DiscordCommand to the database as a valid interaction
                    try {
                        discordCommand.WriteToDb(db, this.admin_id);
                    } catch(SqliteException e){
                        Console.WriteLine($"WebEndpoint: DiscordCommand not saved. Exception: {e.Message}");
                    }
                    
                    //Parse the DiscordCommand and save it as a valid interaction
                    string respString = "";
                    switch(discordCommand.command_id){
                        //Roll command
                        case 1:
                            respString = LogService.ParseRollCommand(discordCommand.interaction_data, (ulong) discordCommand.interaction_id, db);
                            break;
                        case 2:
                            respString = LogService.ParseStatCommand(discordCommand.interaction_data, this.admin_id, db);
                            break;
                        default:
                            respString = $"Invalid command type ({discordCommand.command_id})";
                            break;
                    }
                    discordResponse = new(true,
                        respString);
                }

                Console.WriteLine("Sent response to client.");
                this.WriteStream(discordResponse.ToJson());
            }
        } //End infinite while loop
    }
}
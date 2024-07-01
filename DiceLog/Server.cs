using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

/*
    The Server class establishes a DiceLog server that connects to the provided SQLite database and interacts with a compatible client over HTTPS.
*/
class Server
{
    //The SQLdb object for this server's database connexion
    private SQLdb db {get;}

    //The port number that this server will use
    private Int32 port {get;}

    //The TCP Listener for this server. It will receive incoming TCP connexions on the specified port.
    private TcpListener listener;

    //The certificate that this server will use
    private X509Certificate serverCert;

    /*
        Create a server with the given database name and port number. If given an invalid port number, use the default configured value.
        Port numbers are valid if they fall within the range [49152, 65535] and invalid otherwise. This range avoids reserved ports.
        NOTE: This constructor does not actually start the listener.
    */
    public Server(SQLdb db, int port){
        this.db = db;

        //Check validity of input port number or use the configured default port number
        if(port >= 49152 && port <= 65535){
            this.port = port;
        } else {
            this.port = LogService.configs["ServerPort"];
        }

        //Resolve this device's address. This is equivalent to using localhost as the specified IP address.
        IPAddress localhost = IPAddress.Parse("127.0.0.1");
        this.listener = new TcpListener(localhost, this.port);

        //Get the certificate from the file specified in .config
        //this.serverCert = X509Certificate.CreateFromCertFile(LogService.configs["CertificateFile"]);
        this.serverCert = new X509Certificate(LogService.configs["CertificateFile"], LogService.configs["CertificatePassword"]);
    }

    //Default constructor (uses default database name and port number)
    //public Server(): this(LogService.configs["DatabaseFile"], -1){}

    /*
        Start the server and listen for client connexions. Once started, this function will run indefinitely. To close the server, use CTRL+C.
    */
    public void RunServer(){
        this.listener.Start();
        Console.WriteLine("Server started.");

        //Infinite loop to listen for clients
        while(true){
            Console.WriteLine("Waiting for client connexion...");

            try {
                //Accept client
                TcpClient client = listener.AcceptTcpClient();

                //If client is null, ignore this connexion
                if(client == null || client.Client == null || client.Client.RemoteEndPoint == null) continue;

                //IP address and port of client
                string clientIP = IPAddress.Parse(((IPEndPoint) client.Client.RemoteEndPoint).Address.ToString()).ToString();
                string clientPort = ((IPEndPoint) client.Client.RemoteEndPoint).Port.ToString();

                Console.WriteLine($"Connexion received from {clientIP}:{clientPort}. Authenticating...");

                //Hand client off to WebEndpoint
                var endpoint = new WebEndpoint(this.db, client, this.serverCert, clientIP, clientPort);

                //If ProcessTraffic crashes, keep the server going and wait for a new client
                try {
                    endpoint.ProcessTraffic();
                } catch (System.IO.IOException e){
                    Console.WriteLine($"Socket Exception: {e.Message}");
                    continue;
                }
            } catch (Exception e){
                Console.WriteLine($"Bad client connexion. Exception: {e.Message}");
            }
        }
    }
}
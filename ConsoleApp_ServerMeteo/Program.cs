using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.ComponentModel;

/*
 * Studenti: Andrea Maria Castronovo e Francesco Caravita
 * Classe: 5I
 * Gruppo: 3
 * Data: 21/02/2024
 * Descrizione: Codice server per progetto Stazione meteo
 */

namespace ConsoleApp_ServerMeteo
{
    internal class Program
    {
        static void Masin(string[] args)
        {
            MySqlConnection conn;

            Dictionary<string, string> databaseCredentials = new Dictionary<string, string>
            {
                {"Server","localhost" },
                {"Database","Meteo_5I_06"},
                {"Uid","root"},
                {"Pwd","burbero2023"}
            };

            string connectionString = $"Server={databaseCredentials["Server"]};Database={databaseCredentials["Database"]};Uid={databaseCredentials["Uid"]};Pwd={databaseCredentials["Pwd"]};";
            
            conn = new MySqlConnection(); // Serve per gestire la CONNESSIONE con il DB
            conn.ConnectionString = connectionString;
            conn.Open();

            MySqlCommand cmd = new MySqlCommand(); // Serve per eseguire dei comandi SQL
            cmd.Connection = conn;
            cmd.CommandText = "SELECT * FROM Sensori;";
        }
        static void Main(string[] args)
        {

            const string QUERY_COUNT = "SELECT COUNT(*) FROM sensori WHERE idCodiceSensore = {0}";
            const string QUERY_CAM ="SELECT COUNT(*) FROM Sensori, SensoriInstallati " +
                "WHERE sensori.Camera = 1 AND idSensoriInstallati = {0} AND Sensori.idCodiceSensore = SensoriInstallati.idCodiceSensore";

            const string QUERY_STAZIONE =
                "SELECT idNomeStazione FROM idSensoriInstallati = {0}";

            const string QUERY_INSERT_IMMAGINE =
                "INSERT INTO Rilevamenti(idSensoriInstallati, DataOra, Dato) " +
                "VALUES({0},{1},{2}); " +
                "SELECT LAST_INSERT_ID() Rilevamenti;"; // Questa query ha 2 comandi, il primo inserisce il nuovo dato, il secondo ottiene l'ID del dato che ha appena inserito

            const string QUERY_SENSORE_NORMALE = "INSERT INTO Rilevamenti(idSensoriInstallati, DataOra, Dato) " +
                "VALUES({0},'{1} {2}','{3}')";

            

            // Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;
            MySqlConnection conn;

            Dictionary<string, string> databaseCredentials = new Dictionary<string, string>
            {
                {"Server","localhost" },
                {"Database","Meteo_5I_06"},
                {"Uid","root"},
                {"Pwd","burbero2023"}
            };

            string connectionString = $"Server={databaseCredentials["Server"]};Database={databaseCredentials["Database"]};Uid={databaseCredentials["Uid"]};Pwd={databaseCredentials["Pwd"]};";

            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPAddress iPAddress = IPAddress.Parse("10.1.0.6");

            IPEndPoint localEP = new IPEndPoint(iPAddress, 11000);

            try
            {
                listener.Bind(localEP);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("\n INDIRIZZO IP NON VALIDO");
                Console.WriteLine(" Premi \"invio\" per terminare il programma");
                Console.ReadLine();
                return;
            }

            listener.Listen(10);

            while (true)
            {
                Console.WriteLine("\n\nAttesa connessione...");

                Socket handler = listener.Accept();

                string ip = handler.RemoteEndPoint.ToString().Split(':')[0]; // Ip che sta effettuando la richiesta al server
                
                List<string> foo = new List<string>(); // Questa dovrebbe essere la lista degli ip consentiti che abbiamo sul DB
                foo.Add("10.1.100.5");

                bool ok = false;
                foreach (string myIp in foo) // Controllo se l'ip che ha fatto la richiesta è negli ip consentiti
                {
                    if (myIp == ip)
                        ok = true;
                }

                if (!ok)
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    Console.WriteLine($"\n\n CONNESSIONE RIFIUTATA IP = {ip}");
                    continue;
                }

                DateTime dataOraDiSistema = DateTime.Now;
                Console.WriteLine($"[{dataOraDiSistema:dd/MM/yyyy HH:mm:ss}] - Connessione accettata");

                int bytesRec;
                string strJson = "";
                byte[] buffer = new byte[20000];

                do
                {
                    bytesRec = handler.Receive(buffer, 1, SocketFlags.Partial);
                    strJson += Encoding.ASCII.GetString(buffer, 0, bytesRec);
                } while (!strJson.EndsWith("]"));

                Console.WriteLine($"Dati ricevuti:\n{strJson}");

                // Convertiamo il JSON in una lista di oggetti della classe fatta prima
                List<DatoSensore> listaSensori =
                    JsonConvert.DeserializeObject<List<DatoSensore>>(strJson);


                try
                {
                    conn = new MySqlConnection(); // Serve per gestire la CONNESSIONE con il DB
                    conn.ConnectionString = connectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand(); // Serve per eseguire dei comandi SQL
                    cmd.Connection = conn;
                    
                    foreach (DatoSensore sensor in listaSensori)
                    {
                        Console.WriteLine($"Archivio valore sensore ID = {sensor.IDSensore}");
                        
                        string sql = string.Format(QUERY_COUNT, sensor.IDSensore.ToString());
                        
                        cmd.CommandText = sql;
                        
                        int ni = Convert.ToInt32(cmd.ExecuteScalar()); // Excecute scalar esegue il comando e
                                                                       // resetituisce la cella 0,0 del risultato
                                                                       // della query
                        // Nella variabile "ni" ci sarà quindi il risultato di COUNT(*)
                        if (ni == 1) // VALIDAZIONE: Se è 1, allora il sensore è presente nel DB
                        {
                            
                            string dataOraServer = $"{DateTime.Now:yyyy/MM/dd HH:mm}";
                            string dataOraClient = sensor.data + " " + sensor.ora.Remove(5);
                            // Servirebbe anche il fuso orario, ma possiamo ricavarlo perché abbiamo le
                            // coordinate della stazione meteo.

                            if (dataOraServer != dataOraClient) 
                            {
                                sensor.data = dataOraServer.Split(' ')[0];
                                sensor.ora = dataOraServer.Split(' ')[1];
                                Console.WriteLine("\nERRORE: Data e Ora del client non corrispondono con quelle del server.");
                            }

                            cmd.CommandText = string.Format(QUERY_CAM, sensor.IDSensore.ToString()); // Non è stata parametrizzata perché siamo sicuri della provenienza dei dati.
                            int nc = Convert.ToInt32(cmd.ExecuteScalar());

                            if (nc == 1)
                            { // Se entra qui è una CAM, devo quindi creare o ottenere la cartella per le immagini

                                cmd.CommandText = string.Format(QUERY_STAZIONE, sensor.IDSensore);
                                string codStaz = cmd.ExecuteScalar().ToString();

                                // Dobbiamo creare una cartella che immagazzinerà le immagini
                                // catturate dalla stazione in questione, per cui verifichiamo
                                // l'esistenza di una cartella che differenzi la stazione...
                                if (!Directory.Exists(codStaz))
                                {
                                    Directory.CreateDirectory(codStaz); // ... e se non c'è la creiamo
                                }


                                cmd.CommandText = string.Format(QUERY_INSERT_IMMAGINE, sensor.IDSensore, $"'{sensor.data} {sensor.ora}'", sensor.valore.ToString());
                                ulong idIdentity = (ulong)cmd.ExecuteScalar(); // Qui (a differenza di dopo) non usiamo ExecuteNonQuery perché al seconda parte della query fatta restituisce un risultato.

                                int numByte = Convert.ToInt32(sensor.valore); // Nel caso di una cam sappiamo che il valore era un intero, per cui lo riconvertiamo
                                byte[] imageBuffer = new byte[numByte];


                                // L'immagine potrebbe richiedere più di una trasmissione per via della sua grandezza,
                                // cicliamo quindi finché non abbiamo ricevuto tutto.
                                int attBytesRec = 0;
                                do
                                {
                                    attBytesRec += 
                                        handler.Receive(imageBuffer, attBytesRec, numByte - attBytesRec, SocketFlags.None);
                                } while (attBytesRec < numByte);

                                string percorso = 
                                    $".\\{codStaz}\\{sensor.dataConOra()}+{idIdentity}.jpg"; // Per favorire l'accesso alla cartella
                                                                                             // anche da un umano mettiamo la data e l'ora
                                                                                             // oltre al codice univoco dell'immagine
                                File.WriteAllBytes(percorso, imageBuffer); // Salviamo i byte ricevuti nel file.
                                Console.WriteLine("File salvato: "+percorso);
                            }
                            else
                            { // Entra qui quando è un sensore normale (no CAM)

                                cmd.CommandText = string.Format(QUERY_SENSORE_NORMALE, 
                                    sensor.IDSensore.ToString(),
                                    sensor.data, 
                                    sensor.ora, 
                                    sensor.valore
                                );
                                cmd.ExecuteNonQuery(); // Essendo che dobbiamo fare un INSERT, che non restituisce nulla, usiamo NON QUERY.
                            }
                        }
                        else
                        {
                            Console.WriteLine($"ERRORE: sensore {sensor.IDSensore} non ");
                        }
                    }
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"{ex}");
                }
            }


        }
    }
}

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
        static void Main(string[] args)
        {

            const string QUERY_COUNT = "SELECT COUNT(*) FROM sensoriinstallati WHERE idSensoriInstallati={0}";
            const string QUERY_CAM =
                "SELECT COUNT(*) FROM meteodb.sensori, meteodb.sensoriinstallati.idCodiceSensore " +
                "AND Camera = TRUE AND sensoriinstallati.idSensoriInstallati = {0}";

            const string QUERY_STAZIONE =
                "SELECT idNomeStazione FROM meteodb.sensoriinstallati.idSensoriInstallati = {0}";

            const string QUERY_INSERT_IMMAGINE =
                "INSERT INTO rilevamenti(idSensoriInstallati, DataOra, Dato) " +
                "VALUES({0},{1},{2}); " +
                "SELECT LAST_INSERT_ID() rilevamenti;"; // Questa query ha 2 comandi, il primo inserisce il nuovo dato, il secondo ottiene l'ID del dato che ha appena inserito

            // Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;
            MySqlConnection conn;

            Dictionary<string, string> databaseCredentials = new Dictionary<string, string>
            {
                {"Server","MyServer" },
                { "Database","myDataBase"},
                {"Uid","myUsername"},
                {"Pwd","myPassword"}
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


                bool ok = false;
                foreach (string myIp in foo) // Controllo se l'ip che ha fatto la richiesta è negli ip consentiti
                {
                    if (myIp == ip)
                        ok = true;
                }

                if (ok)
                {
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                    Console.WriteLine($"\n\n CONNESSIONE RIRIUFATA IP = {ip}");
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
                        if (ni == 1) // Se è 1, allora il sensore è presente nel DB
                        {
                            cmd.CommandText = string.Format(QUERY_CAM, sensor.IDSensore.ToString()); // Non è stata parametrizzata perché siamo sicuri della provenienza dei dati.
                            int nc = Convert.ToInt32(cmd.ExecuteScalar());

                            if (nc == 1)
                            { // Se entra qui è una CAM, devo quindi creare o ottenere la cartella per le immagini
                                string codStaz = cmd.ExecuteScalar().ToString();
                                if (!Directory.Exists(codStaz))
                                {
                                    Directory.CreateDirectory(codStaz);
                                }

                                cmd.CommandText = string.Format(QUERY_INSERT_IMMAGINE, sensor.IDSensore, $"'{sensor.data} {sensor.ora}'", sensor.valore.ToString());
                                ulong idIdentity = (ulong)cmd.ExecuteScalar();

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

                                string percorso = $".\\{codStaz}\\{sensor.dataConOra()}+{idIdentity}.jpg";
                                System.IO.File.WriteAllBytes(percorso, imageBuffer);
                                Console.WriteLine("File salvato: "+percorso);
                            }
                        }
                    }
                }
                catch (Exception ex) 
                {
                    
                }
            }


        }
    }
}

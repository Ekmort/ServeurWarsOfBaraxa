﻿using System;
using System.Data;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using Oracle.DataAccess.Client;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using warsofbaraxa;
using WarsOfBaraxaBD;

namespace ServeurWarsOfBaraxa
{
    class Client
    {
        static Socket sck;
        static AccesBD acces;
        static private OracleConnection conn;
        static private String connexionChaine;
        private static OracleDataReader dataReader;
        public Client(Socket socket)
        { 
            sck=socket;
        }
        public void doWork()
        {
            acces = new AccesBD();
            acces.Connection();
            bool Deconnection = false;
            bool partieCommencer=false;
            while (!Deconnection)
            {
                if (!partieCommencer)
                {
                    string message=recevoirResultat(sck);
                    string[] data= message.Split(new char[]{','});
                    if(data.Length==2)
                    {
                        if(acces.estPresent(data[0],data[1]))
                        {
                            sendClient(sck,"oui");
                        }
                        else
                        {
                            sendClient(sck, "non");
                        }
                    }
                    else if (data.Length==4)
                    {
                        if (acces.estDejaPresent(data[0]))
                        {
                            sendClient(sck, "oui");
                        }
                        else
                        {
                            sendClient(sck, "non");
                            acces.ajouter(data[0], data[1], data[2], data[3]);

                        }
                    }
                    else 
                    {
                        partieCommencer = true;
                    }
                }
                else
                { 
                    
                }
            }
        }
        static private void sendDeckJoueurClient()
        {
            acces.Connection();
            Carte[] CarteJoueur = acces.ListerDeckJoueur("ekmort",1);
            Deck DeckJoueur = new Deck(CarteJoueur);
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("172.17.104.127"), 1234);
            try
            {
                sck.Connect(localEndPoint);
            }
            catch
            {
                System.Console.Write("Erreur de connexion");
            }

            if (sck.Connected)
            {
                byte[] data;
                BinaryFormatter b = new BinaryFormatter();
                using (var stream = new MemoryStream())
                {
                    b.Serialize(stream, DeckJoueur);
                    data = stream.ToArray();
                }
                sck.Send(data);
            }
        }
        private static string recevoirResultat(Socket client)
        {
            string strData = "";
            try
            {
                byte[] buff = new byte[client.SendBufferSize];
                int bytesRead = sck.Receive(buff);
                byte[] formatted = new byte[bytesRead];
                for (int i = 0; i < bytesRead; i++)
                {
                    formatted[i] = buff[i];
                }
                strData = Encoding.ASCII.GetString(formatted);
            }
            //ne fait rien car le client n'a rien envoyer parcequ'il c'est déconnecter
            catch (SocketException sock) { }
            return strData;
        }
        private static void sendClient(Socket client, String text)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(text);
                client.Send(data);
            }
            catch { Console.Write("Erreur de telechargement des donnees"); }
        }
    }
}
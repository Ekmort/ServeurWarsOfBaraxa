using System;
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
        private Joueur Moi;
        private Joueur Ennemis;
        AccesBD acces;
        private OracleConnection conn;
        private String connexionChaine;
        private OracleDataReader dataReader;
        private bool Deconnection = false;
        private bool partieCommencer = false;
        bool Debut = true;
        private int posClient;
        public Client(Joueur temp)
        {
            
            Moi = temp;
            Deconnection = false;
            partieCommencer = false;
          
        }
        public void doWork()
        {
            acces = new AccesBD();
            acces.Connection();
            while (!Deconnection)
            {
                if (!partieCommencer)
                {
                    string message=recevoirResultat(Moi.sckJoueur);
                    string[] data= message.Split(new char[]{','});
                    TraiterMessageAvantPartie(data);
                }
                else
                {
                    if(Debut)
                    AvantMatch();

                    if (Moi.Depart)
                    {
                        DebutTour();
                        Tour();
                        if (aPerdu(Moi))
                        {
                            partieCommencer = false;
                            sendClient(Moi.sckJoueur, "vous avez perdu");
                            acces.AjouterDefaite(Moi.nom);
                        }
                        else if (aPerdu(Ennemis))
                        {
                            sendClient(Moi.sckJoueur, "vous avez gagné");
                            acces.AjouterVictoire(Moi.nom);
                            partieCommencer = false;
                        }
                    }

                }
            }
        }
        //trouve le joueur et lui permet de mulligan(pas encore fait le mulligan)
        private void AvantMatch()
        {
            getFirstPlayer();
            //mulligan but not now
            Debut = false;
            Serveur.messageEnnemis = "Joueur2";
        }
        //pige une carte et apres fait les trigger de carte si il y en a(rien de faite)
        private void DebutTour()
        { 
            //sendClient(Ennemis.sckJoueur,,"piger");   A FAIRE!!!!!
            //if ability trigger ds debut tour faire
            //then tour
        }
        private void Tour()
        {
            bool FinTour = false;
            while (!FinTour)
            {
                string message = recevoirResultat(Moi.sckJoueur);
                string[] data = message.Split(new char[] { ',' });
                traiterMessagePartie(data);
            };
        }
        private void traiterMessagePartie(string[] data)
        {
            switch (data[0])
            { 
                case "Ajouter Mana":
                    setMana(Moi,int.Parse(data[1]),int.Parse(data[2]),int.Parse(data[3]));
                    sendClient(Ennemis.sckJoueur,"AjouterManaEnnemis,"+Moi.nbBle+","+ Moi.nbBois+","+ Moi.nbGem);
                break;
                case "Jouer Carte":
                    Carte temp = ReceiveCarte(Moi.sckJoueur);
                    EnleverMana(Moi, temp);
                    sendClient(Ennemis.sckJoueur, "AjouterCarteEnnemis," + Moi.nbBle + "," + Moi.nbBois + "," + Moi.nbGem);
                    EnvoyerCarte(Ennemis.sckJoueur,temp);
                break;
                case "Fin De Tour":
                    Moi.Depart = false;
                    Ennemis.Depart = true;
                    sendClient(Ennemis.sckJoueur, "Tour Commencer");
                break;

                case "Attaquer Joueur":
                    Carte attaquant = ReceiveCarte(Moi.sckJoueur);
                    Ennemis.vie -= attaquant.perm.Attaque;
                    sendClient(Ennemis.sckJoueur, "Joueur attaquer," + Ennemis.vie.ToString());
                break;
                case "Attaquer Creature":
                    Carte maCreature = ReceiveCarte(Moi.sckJoueur);
                    Carte saCreature = ReceiveCarte(Moi.sckJoueur);
                break;
            }
        }
        private Carte ReceiveCarte(Socket client)
        {
            Carte carte = null;
            try
            {
                byte [] buffer = new byte[client.SendBufferSize];
                int bytesRead = client.Receive(buffer);
                byte[] formatted = new byte[bytesRead];
                BinaryFormatter receive = new BinaryFormatter();

                for (int i = 0; i < bytesRead; i++)
                {
                    formatted[i] = buffer[i];
                }
                using (var recstream = new MemoryStream(formatted))
                {
                    carte = receive.Deserialize(recstream) as Carte;
                }

            }
            catch { Console.Write("Erreur de telechargement des données"); }
            return carte;
        }
        private void EnvoyerCarte(Socket client, Carte carte)
        {
            byte[] data;
            BinaryFormatter b = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                b.Serialize(stream, carte);
                data = stream.ToArray();
            }
            client.Send(data); 
        }
        private void getFirstPlayer()
        {
            Serveur.mutex.WaitOne();
            if(Serveur.joueurDepart ==0)
                Serveur.joueurDepart = new Random().Next(1,2);
            if (Serveur.joueurDepart == Moi.nbDepart)
            {
                sendClient(Moi.sckJoueur, "Premier Joueur");
                Moi.Depart = true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "Deuxieme Joueur");
                Moi.Depart = false;
            }
            Serveur.mutex.ReleaseMutex();
            Thread.Sleep(500);
            Serveur.joueurDepart = 0;
        }
        private void TraiterMessageAvantPartie(string[] data)
        {
            switch (data.Length)
            {
                case 2:
                if(data[0] == "afficher profil")
                {
                    sendProfil(data[1]);
                }
                else if (estPresent(data))
                   Moi.nom = data[0];
                break;
                case 4:
                if (peutEtreAjouter(data))
                {
                    Moi.nom = data[0];
                    ajouterBasicDeck(Moi.nom);
                }
                break;
            }
            switch (data[0])
            { 
                case "deconnection":
                    Deconnection = true;
                    Serveur.tabJoueur.Remove(Moi);
                break;
                case "recevoir Deck":
                    sendDeck(Moi.nom);
                break;
                case "trouver partie":
                if (startGame(Moi.sckJoueur, posClient))
                partieCommencer = true;
                break;
            }
        }
        private void ajouterBasicDeck(string alias)
        {
            acces.setBasicDeck(alias);
        }
        private bool startGame(Socket client, int pos)
        {
            Serveur.tabPartie.Add(Moi);
            bool rechercher = true;
            Serveur.mutex.WaitOne();
            while (rechercher && Serveur.tabPartie.Count<2)
            {
                sendClient(Moi.sckJoueur, "recherche");
                string message = recevoirResultat(Moi.sckJoueur);
                if (message == "deconnection")
                {
                    rechercher = false;
                    Deconnection = true;
                    Serveur.tabJoueur.Remove(Moi);
                    Serveur.tabPartie.Remove(Moi);
                }
            }
            Serveur.mutex.ReleaseMutex();
            if (rechercher)
            {
                    setPartie();
                    if (Ennemis != null)
                    {
                        Serveur.mutex.WaitOne();
                        Serveur.mutex.ReleaseMutex();
                        Thread.Sleep(1500);
                        Serveur.tabPartie.Remove(Moi);
                        sendClient(Moi.sckJoueur, "Partie Commencer");
                        Serveur.temp1 = null;
                        Serveur.temp2 = null;
                        return true;
                    }
                    else
                    {
                        Serveur.tabPartie.Remove(Moi);
                        startGame(client, pos);
                    }
            }
            else
            {
                return false;
            }
            return true;
        }
        private void EnleverMana(Joueur j, Carte c)
        {
            j.nbBle -= c.CoutBle;
            j.nbBois -= c.CoutBois;
            j.nbGem -= c.CoutGem;
        }
        private void ajouterMana(Joueur j,int ble,int bois, int gem)
        {
            j.nbBle += ble;
            j.nbBois += bois;
            j.nbGem += gem;
        }
        private void setMana(Joueur j,int ble,int bois,int gem)
        {
            j.nbBle = ble;
            j.nbBois = bois;
            j.nbGem = gem;
        }
        private void setPartie()
        {
            Serveur.mutex.WaitOne();
                if (Serveur.temp1 == null && Serveur.tabPartie.Count >= 2)
                    Serveur.temp1 = new Joueur(Serveur.tabPartie[0].nom);
                if (Serveur.temp2 == null && Serveur.temp1.nom != Moi.nom && Serveur.tabPartie.Count >= 2)
                    Serveur.temp2 = new Joueur(Serveur.tabPartie[1].nom);
            Serveur.mutex.ReleaseMutex();
            Thread.Sleep(500);
            if (Serveur.temp1.nom == Moi.nom)
            {
                Ennemis = Serveur.tabPartie[1];
                Moi.nbDepart = 1;
            }
            else if (Serveur.temp2.nom == Moi.nom)
            {
                Ennemis = Serveur.tabPartie[0];
                Moi.nbDepart = 2;
            }
            else
                Ennemis = null;
        }
        private bool estPresent(string[] data)
        {
            if (acces.estPresent(data[0], data[1]) && !estConnecter(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                return true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                return false;
            }        
        }
         private bool estConnecter(string alias)
        {
            for (int i = 0; i < Serveur.tabJoueur.Count; ++i)
            {
                if (Serveur.tabJoueur[i].nom == alias)
                    return true;
            }
            return false;
        }
         private bool peutEtreAjouter(string[] data)
        {
            if (acces.estDejaPresent(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                return false;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                acces.ajouter(data[0], data[1], data[2], data[3]);
                return true;

            }        
        }
        private void sendDeckJoueurClient(String User)
        {
            acces.Connection();
            Carte[] CarteJoueur = acces.ListerDeckJoueur(User,1);
            Deck DeckJoueur = new Deck(CarteJoueur);
            //sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("172.17.104.127"), 1234);
            try
            {
                //sck.Connect(localEndPoint);
            }
            catch
            {
                System.Console.Write("Erreur de connexion");
            }

            /*if (sck.Connected)
            {
                byte[] data;
                BinaryFormatter b = new BinaryFormatter();
                using (var stream = new MemoryStream())
                {
                    b.Serialize(stream, DeckJoueur);
                    data = stream.ToArray();
                }
                sck.Send(data);
            }*/
        }
        private  string recevoirResultat(Socket client)
        {
            string strData = "";
            try
            {
                byte[] buff = new byte[client.SendBufferSize];
                int bytesRead = Moi.sckJoueur.Receive(buff);
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
        private  void sendClient(Socket client, String text)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(text);
                client.Send(data);
            }
            catch { Console.Write("Erreur de telechargement des donnees"); }
        }
        private  void sendProfil(string alias)
        {
            string profile=acces.getProfil(alias);
            sendClient(Moi.sckJoueur, profile);
        }
        private  bool aPerdu(Joueur player)
        { 
            if(player.vie<=0 || player.nbCarteDeck <=0)
            {
                return true;
            }
            return false;
        }
        private  void sendDeck(string alias)
        {
            string deck = acces.getDeckJoueur(alias);
            if (deck != "")
                sendClient(Moi.sckJoueur, deck);
            else
                sendClient(Moi.sckJoueur, "aucun deck");
        }

    }
}

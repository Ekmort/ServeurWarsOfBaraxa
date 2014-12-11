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
        private Deck monDeck;
        private Joueur Ennemis;
        private Thread t;
        ThreadLire ReceiveMessage;
        AccesBD acces;
        private bool Deconnection = false;
        private bool partieCommencer = false;
        bool Debut = true;
        private int posPartie;
        private int posClient;
        private string NomDeck = "";
        public Client(Joueur temp, int posT)
        {
            posPartie = -1;
            Moi = temp;
            Deconnection = false;
            partieCommencer = false;
            posClient = posT;
            ReceiveMessage = new ThreadLire();
            ReceiveMessage.workSocket = Moi.sckJoueur;

        }
        public void doWork()
        {
            //fait une connexion a la BD et l'ouvre
            acces = new AccesBD();
            acces.Connection();
            acces.Open();
            //tant que le joueur ne veut pas se déconnecter
            while (!Deconnection)
            {
                //si ce n'est pas la partie
                if (!partieCommencer)
                {
                    //on recois le message et on le traite
                    string message = recevoirResultat(Moi.sckJoueur);
                    string[] data = message.Split(new char[] { ',' });
                    TraiterMessageAvantPartie(data);
                }
                else
                {
                    //si ce n'est pas le début
                    if (Debut)
                        AvantMatch();
                    //si c'est mon tour
                    if (Moi.Depart)
                    {
                        //on ferme le thread qui vérifie si je veut partir si il est ouvert
                        if (t != null && t.IsAlive)
                        {
                            t.Abort();
                        }
                        //on fait notre tour
                        Tour();
                    }
                    else
                    {
                        //je crée un thread pour vérifier si l'ennemis envoye un message pour quitter le jeu
                        if (t == null || !t.IsAlive)
                        {
                            t = new Thread(ReceiveMessage.doWork);
                            t.Start();
                        }
                        else
                        {
                            //s'il recois quelquechose alors on le traite
                            if (ReceiveMessage.message != "")
                            {
                                traiterMessagePartie(ReceiveMessage.message.Split(new char[] { '.' }));
                                ReceiveMessage.message = "";
                            }
                        }
                        //on vérifie toujours s'il a perdu
                        verifiervictoireEnnemis();
                    }
                }
            }
        }
        //vérifie si j'ai gagner ou perdu retourne true si quelqu'un a gagne
        private bool verifierVictoire()
        {
            if (Moi != null)
            {
                //on vérifie si j'ai perdu
                if (aPerdu(Moi) || Serveur.games[posPartie].PuCarte)
                {
                    //on set les variables et on donne une défaite a la bd
                    partieCommencer = false;
                    acces.AjouterDefaite(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                    return true;
                }

            }
            if (Ennemis != null)
            {
                //on vérifie si j'ai gagné
                if (aPerdu(Ennemis))
                {
                    //on set les variables et on donne une victoire a la bd
                    acces.AjouterVictoire(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                    partieCommencer = false;
                    return true;
                }
            }
            //on vérifie si le joueur est partie
            bool estPartie = JoueurPartie();
            //si oui on quite
            if (estPartie)
                return true;

            return false;
        }
        //vérifie si un joueur est partie
        private bool JoueurPartie()
        {
            //on prend les num des joueurs et on vérifie si quelqu'un est partie
            int num = Serveur.getPosIndex(posClient, posPartie);
            int numEnnemi = Serveur.findEnnemis(posClient, posPartie);
            int indexEnnemi = -1;
            //si on a trouvé un ennemis
            if (numEnnemi != -1)
                indexEnnemi = Serveur.getPosIndex(numEnnemi, posPartie);
            //s'il y a le bon num de joueur(allié et ennemis)
            if (num != -1 && indexEnnemi != -1)
            {
                //si je suis partie
                if (Serveur.games[posPartie].joueurpart[num])
                {
                    //j'ai une defaite et je set les variable a partie terminer
                    acces.AjouterDefaite(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    partieCommencer = false;
                    resetPartie();
                    return true;
                }
                    //si mon ennemis est partie
                else if (Serveur.games[posPartie].joueurpart[indexEnnemi])
                {
                    //j'ai une victoire et je set les varables a partie gagner
                    acces.AjouterVictoire(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    partieCommencer = false;
                    resetPartie();
                    return true;
                }
            }
            return false;
        }
        //recommence la partie(set les variables a leur valeurs de base
        private void resetPartie()
        {
            posPartie = -1;
            NomDeck = "";
            Moi.vie = 30;
            Ennemis.vie = 30;
            monDeck = null;
            Ennemis = null;
            Debut = true;

        }
        //vérifie si quelqu'un a gagner ou perdu
        private void verifiervictoireEnnemis()
        {
            if (Moi != null && Ennemis != null)
            {
                if (aPerdu(Moi))
                {
                    partieCommencer = false;
                    acces.AjouterDefaite(Moi.nom);
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                }
                else if (aPerdu(Ennemis) || Serveur.games[posPartie].PuCarte)
                {
                    acces.AjouterVictoire(Moi.nom);
                    partieCommencer = false;
                    Serveur.games[posPartie].TerminerGame();
                    resetPartie();
                }
                JoueurPartie();
            }
        }
        //trouve le joueur
        private void AvantMatch()
        {
            //envoye le deck au joueur
            sendDeckJoueurClient();
            //attend pour que le client recois tout
            Thread.Sleep(500);
            //envoye le premier joueur
            getFirstPlayer();
            Debut = false;
        }
        //fait les action du joueur pendant un tour
        private void Tour()
        {
            bool FinTour = false;
            //tant que le joueur n'a pas terminer sont tour on attend une action
            while (!FinTour)
            {
                //on recois et traite le message
                string message = recevoirResultat(Moi.sckJoueur);
                string[] data = message.Split(new char[] { '.' });
                traiterMessagePartie(data);
                //vérifie s'il y a une victoire ou défaite
                FinTour = verifierVictoire();
                //vérifie s'il y a une fin de tour
                if (!FinTour && data[0] == "Fin De Tour")
                    FinTour = true;
            }
        }
        //si un sort vise le héros
        private bool ifHeroDmg(string[] textHabilete)
        {
            bool valide = false; 
            int i = 0;
            //on vérifie chaque mot pour tourever si le héros est viser par le sort 
            while (!valide && i < textHabilete.Length)
            {
                //si oui on retourne que la cible est valide
                if (textHabilete[i] == "aux" && textHabilete[i + 1] == "ennemis")
                    valide = true;
                else if (textHabilete[i] == "place" && textHabilete[i + 1] == "de" && textHabilete[i + 2] == "combat")
                    valide = true;
                i++;
            }
            return valide;
        }
        //si le sort touche sont propre héros
        private bool ifSelfHeroDmg(string[] textHabilete)
        {
            bool valide = false;
            int i = 0;
            //on vérifie chaque mot pour tourever si le héros est viser par le sort 
            while (!valide && i < textHabilete.Length)
            {
                //si oui on retourne que la cible est valide
                if (textHabilete[i] == "place" && textHabilete[i + 1] == "de" && textHabilete[i + 2] == "combat")
                    valide = true;
                i++;
            }
            return valide;
        }
        //traite les messages de la partie
        private void traiterMessagePartie(string[] data)
        {
            //on vérifie la premiere donnée pour savoir ce que le joueur veut faire
            switch (data[0])
            {
                case "Ajouter Mana":
                    //on set la mana a ces nouvelles valeur
                    setMana(Moi, int.Parse(data[1]), int.Parse(data[2]), int.Parse(data[3]));
                    //on le dit au joueur ennemis
                    sendClient(Ennemis.sckJoueur, "AjouterManaEnnemis." + Moi.nbBle + "." + Moi.nbBois + "." + Moi.nbGem);
                    break;
                case "Jouer spellnotarget":
                    //on recois la carte
                    Carte zeSpell = createCarte(data, 2);
                    //on réduis la mana du joueur ennemis
                    EnleverMana(Moi, zeSpell);
                    //selon le type de sort on fait quelquechose de différent
                    if (zeSpell.Habilete.Split(new char[] { ' ' })[0] == "Inflige")
                    {
                        //si le sort cible le héros ennemis on réduis sa vie
                        if (ifHeroDmg(zeSpell.Habilete.Split(new char[] { ' ' })))
                            Ennemis.vie -= int.Parse(zeSpell.Habilete.Split(new char[] { ' ' })[1]);
                        //si le sort cible le héros allié on réduis notre vie
                        if (ifSelfHeroDmg(zeSpell.Habilete.Split(new char[] { ' ' })))
                            Moi.vie -= int.Parse(zeSpell.Habilete.Split(new char[] { ' ' })[1]);
                    }
                    //on créer un string avec le spell pour l'envoye au joueur ennemis
                    string spellString = SetCarteString(zeSpell);
                    //envoye les données au joueur ennemis
                    sendClient(Ennemis.sckJoueur, "spellNoTarget." + data[1] + "." + spellString);
                    break;
                case "Jouer spellTarget":
                    //on recois la carte
                    Carte zeSpelltarget = createCarte(data, 3);
                    //on enleve la mana selon le cout du sort
                    EnleverMana(Moi, zeSpelltarget);
                    //si la cible est un héros
                    if (data[2] == "hero ennemis" || data[2] == "hero")
                    {
                        //si c'est du dégats
                        if (zeSpelltarget.Habilete.Split(new char[] { ' ' })[0] == "Inflige")
                        {
                            //nous on enleve notre vie
                            if (data[2] == "hero")
                                Moi.vie -= int.Parse(zeSpelltarget.Habilete.Split(new char[] { ' ' })[1]);
                                //sinon on enleve le dégats ennemis
                            else
                                Ennemis.vie -= int.Parse(zeSpelltarget.Habilete.Split(new char[] { ' ' })[1]);
                            //on créer un string avec le spell pour l'envoye au joueur ennemis
                            string spelltargetString = SetCarteString(zeSpelltarget);
                            //envoye les données au joueur ennemis
                            sendClient(Ennemis.sckJoueur, "spellwithtarget." + data[1] + "." + data[2] + "." + spelltargetString);
                        }
                    }
                        //la vible est une créature
                    else
                    {
                        //on crée la carte target déjà modifié par le client
                        Carte zeTarget = createCarte(data,10);
                        //on fait des string avec les deux carte
                        string spelltargetString = SetCarteString(zeSpelltarget);
                        string targetString = SetCarteString(zeTarget);
                        //on l'envoie au joueur ennemis
                        sendClient(Ennemis.sckJoueur, "spellwithtarget." + data[1] + "." + data[2] + "." + spelltargetString + "." + targetString);
                    }
                    break;
                case "Jouer Carte":
                    //on recois la carte
                    Carte temp = ReceiveCarte(Moi.sckJoueur);
                    //on enleve la mana
                    EnleverMana(Moi, temp);
                    //on créer un string
                    string fckingCarte = SetCarteString(temp);
                    //on l'envoie a l'ennemis
                    sendClient(Ennemis.sckJoueur, "AjouterCarteEnnemis." + data[1] + "." + fckingCarte);
                    break;
                case "Fin De Tour":
                    //on donne le tour a l'ennemis et on dit que ce n'est pas notre tour
                    Moi.Depart = false;
                    Ennemis.Depart = true;
                    //on l'enovie au joueur ennemis que c'est son tour
                    sendClient(Ennemis.sckJoueur, "Tour Commencer");
                    break;

                case "Attaquer Joueur":
                    //on receois l'attaquant
                    Carte attaquant = ReceiveCarte(Moi.sckJoueur);
                    setHabilete(attaquant);
                    //si l'attaquant a attaque puissante on enleve le double de dégat
                    if (attaquant.perm.estAttaquePuisante)
                        Ennemis.vie -= attaquant.perm.Attaque * 2;
                        //sinon on réduit la vie selon le dégat de la créature
                    else
                        Ennemis.vie -= attaquant.perm.Attaque;
                    //on envoie la nouvelle vie au joeuur
                    sendClient(Ennemis.sckJoueur, "Joueur attaquer." + Ennemis.vie.ToString());
                    break;
                case "Attaquer Creature":
                    //on envoie au joueur adverse a l'autre joueur
                    sendClient(Ennemis.sckJoueur, "Combat Creature." + data[1] + "." + data[2] + "." + data[3] + "." + data[4]
                    + "." + data[5] + "." + data[6] + "." + data[7] + "." + data[8] + "." + data[9] + "." + data[10] + "." + data[11] + "." + data[12] + "." + data[13] + "." + data[14] + "." + data[15]
                    + "." + data[16] + "." + data[17] + "." + data[18] + "." + data[19] + "." + data[20] + "." + data[21] + "." + data[22] + "." + data[23] + "." + data[24] + "." + data[25] + "." + data[26]);
                    break;
                case "Piger":
                    //on dit au joueur adverse que l'on pige
                    sendClient(Ennemis.sckJoueur, "Ennemis pige");
                    break;
                case "Carte manquante":
                    //on envoie au joueur que nous n'avons plus de carte
                    sendClient(Ennemis.sckJoueur, "Carte manquante");
                    Serveur.games[posPartie].PuCarte = true;
                    break;
                case "deconnection":
                    //on s'enleve du tabjoueur et on dit que l'on veut se deconnecter
                    Deconnection = true;
                    int num = Serveur.getPosIndex(posClient, posPartie);
                    if (num != -1)
                        Serveur.JoueurPart(num, posPartie);
                    Serveur.tabJoueur[posClient] = null;
                    //on le dit au joueur ennemis que on part
                    sendClient(Ennemis.sckJoueur, "JePart");
                    break;
                case "surrender":
                    //on part de la partie
                    partieCommencer = false;
                    int numsurrender = Serveur.getPosIndex(posClient, posPartie);
                    if (numsurrender != -1)
                        Serveur.JoueurPart(numsurrender, posPartie);
                    //on le dit au joeuur adverse
                    sendClient(Ennemis.sckJoueur, "JePart");
                    break;
            }

       }
        //crée une carte a partir d'un string
        private Carte createCarte(string[] data, int posDepart)
        {
            Carte zeCarte = null;
            //on vérifie si il' y a assez de donnée pour faire une carte
            if (data.Length >= posDepart + 6)
            {
                //on la crée la carte
                zeCarte = new Carte(int.Parse(data[posDepart + 6]), data[posDepart + 5], data[posDepart + 4], data[posDepart + 3], int.Parse(data[posDepart]), int.Parse(data[posDepart + 1]), int.Parse(data[posDepart + 2]));
                //si c'est un permanent on crée le permanent
                if (zeCarte.TypeCarte == "Permanents" || zeCarte.TypeCarte == "creature" || zeCarte.TypeCarte == "batiment" || zeCarte.TypeCarte == "Permanent")
                    zeCarte.perm = new Permanent(data[posDepart + 10], int.Parse(data[posDepart + 7]), int.Parse(data[posDepart + 8]), int.Parse(data[posDepart + 9]));
            }
            return zeCarte;
        }
        //set les habileté au permanent
        private Carte setHabilete(Carte card)
        {
            if (card.Habilete != "" && card.Habilete != null)
            {
                //vérifie chaque mot pour voir si il y a un habileté de créature
                string[] data = card.Habilete.Split(new char[] { ',' });
                for (int i = 0; i < data.Length; ++i)
                {
                    if (card.esthabileteNormal(data[i]))
                        card.setHabileteNormal(data[i]);
                }
            }
            return card;
        }
        //crée un string a partir d'une carte
        private string SetCarteString(Carte temp)
        {
            if (temp != null)
            {
                //si il n'y a pas de perm
                if (temp.perm != null)
                    /*0                 1               2                   3                   4               5                   6                     7                 8                   9               10*/
                    return temp.CoutBle + "." + temp.CoutBois + "." + temp.CoutGem + "." + temp.Habilete + "." + temp.TypeCarte + "." + temp.NomCarte + "." + temp.NoCarte + "." + temp.perm.Attaque + "." + temp.perm.Vie + "." + temp.perm.Armure + "." + temp.perm.TypePerm;
                else
                    return temp.CoutBle + "." + temp.CoutBois + "." + temp.CoutGem + "." + temp.Habilete + "." + temp.TypeCarte + "." + temp.NomCarte + "." + temp.NoCarte;
            }
            return "";
        }
        //recevoir une carte a partir d'un socket
        private Carte ReceiveCarte(Socket client)
        {
            bool Arecu = false;
            int atemp = 0;
            Carte carte = null;
            while (!Arecu && atemp < 2)
            {
                try
                {
                    //recevoir
                    byte[] buffer = new byte[client.SendBufferSize];
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
                    Arecu = true;
                }
                    //si il ne peut pas recevoir on le deconeecet du serveur
                catch
                {
                    if (atemp == 2)
                    {
                        Serveur.games[posPartie].mutpartie.WaitOne();
                        Console.Write("Erreur de telechargement des donnees");
                        Serveur.tabJoueur.Remove(Moi);
                        Deconnection = true;
                        Serveur.games[posPartie].mutpartie.ReleaseMutex();
                    }
                    else
                    {
                        atemp++;
                    }
                }
            }
            return carte;
        }
        //envoye le deck au joueur
        private void envoyerDeck(Socket client, Deck leDeck)
        {
            byte[] data;
            BinaryFormatter b = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                b.Serialize(stream, leDeck);
                data = stream.ToArray();
            }
            client.Send(data);
        }
        //trouve le premier joueur de la partie
        private void getFirstPlayer()
        {
            //on set le random entre 1 et 2
            Serveur.games[posPartie].mutpartie.WaitOne();
            Serveur.games[posPartie].setRandom();
            Serveur.games[posPartie].mutpartie.ReleaseMutex();
            //on prend notre positon
            int posIndex = Serveur.getPosIndex(posClient, posPartie);
            //si c'est nous on est le premier sinon on est le deuxieme
            if (Serveur.games[posPartie].rand == posIndex + 1)
            {
                sendClient(Moi.sckJoueur, "Premier Joueur");
                Moi.Depart = true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "Deuxieme Joueur");
                Moi.Depart = false;
            }
        }
        //traite les messages avant la partie
        private void TraiterMessageAvantPartie(string[] data)
        {
            switch (data.Length)
            {
                case 2:
                    //affiche notre profil
                    if (data[0] == "afficher profil")
                    {
                        sendProfil(data[1]);
                    }
                        //affiche le profil d'un autre joueur
                    else if (data[0] == "afficher profil Joueur")
                    {
                        sendProfil(data[1]);
                    }
                        //si le joueur est présent dans la BD
                    else if (estPresent(data))
                    {
                        Moi.nom = data[0];
                        Console.WriteLine("Joueur connecté:" + Moi.nom + " , " + Moi.sckJoueur.RemoteEndPoint.ToString() + " , " + posClient);
                    }
                    break;
                case 4:
                    //si le joueur n'est pas présent de la BD
                    if (peutEtreAjouter(data))
                    {
                        Moi.nom = data[0];
                        ajouterBasicDeck(Moi.nom);
                        Console.WriteLine("Joueur connecté:" + Moi.nom + "," + Moi.sckJoueur.RemoteEndPoint.ToString());
                    }
                    break;
            }
            switch (data[0])
            {
                    //le joueur veut se deconnecter
                case "deconnection":
                    Deconnection = true;
                    Serveur.tabJoueur[posClient] = null;
                    Moi = null;
                    break;
                case "recevoir Deck":
                    sendDeck(Moi.nom);
                    break;
                case "trouver partie":
                    //on recois le nom du deck
                    NomDeck = data[1];
                    int numDeck = acces.getNoDeck(NomDeck);
                    if (numDeck != -1)
                    {
                        //on ferme et ouvre la connexion,car il se peut que la conn manque de ressource et ne renvoie pas toute les cartes
                        Serveur.mutPartie1.WaitOne();
                        acces.close();
                        acces.Open();
                        //on recoisla liste de carte et on le transforme en deck
                        Carte[] CarteJoueur = acces.ListerDeckJoueur(Moi.nom, numDeck);
                        if (CarteJoueur != null && CarteJoueur.Length == 40)
                            monDeck = new Deck(CarteJoueur);
                        Serveur.mutPartie1.ReleaseMutex();
                    }
                    if (startGame(posClient))
                        partieCommencer = true;
                    break;
                case "RetourMenu":
                    //revenir au menu de connexion
                    Moi.nom = "Joueur";
                    break;
            }
        }
        //si on ce créer un compte on lui donne les deck de base
        private void ajouterBasicDeck(string alias)
        {
            acces.setBasicDeck(alias);
        }
        
        private bool startGame(int pos)
        {
            //chaque utilisateur s'ajoute dans une partie
            Serveur.mutPartie2.WaitOne();
            posPartie = Serveur.AjouterJoueurPartie(pos);
            Serveur.mutPartie2.ReleaseMutex();
            bool rechercher = true;
            Serveur.mutex.WaitOne();
            //si il manque un joueur on attend un autre joueur
            while (rechercher && !Serveur.partieComplete(posPartie))
            {
                sendClient(Moi.sckJoueur, "recherche");
                string message = recevoirResultat(Moi.sckJoueur);
                //si le joueur veut partir pendant le loading
                if (message == "deconnection")
                {
                    rechercher = false;
                    Deconnection = true;
                    Serveur.tabJoueur[posClient] = null;
                    Serveur.enleverJoueurPartie(pos);
                }
            }
            Serveur.mutex.ReleaseMutex();
            if (rechercher)
            {
                //on place les 2 joueurs ensemble
                setPartie();
                if (Ennemis != null)
                {
                    sendClient(Moi.sckJoueur, "Partie Commencer");
                    return true;
                }
                else
                {
                    startGame(pos);
                }
            }
            else
            {
                return false;
            }
            return true;
        }
        //enleve la man du joueur selon la carte
        private void EnleverMana(Joueur j, Carte c)
        {
            j.nbBle -= c.CoutBle;
            j.nbBois -= c.CoutBois;
            j.nbGem -= c.CoutGem;
        }
        //ajoute la mana selon les valeurs onnée
        private void ajouterMana(Joueur j, int ble, int bois, int gem)
        {
            j.nbBle += ble;
            j.nbBois += bois;
            j.nbGem += gem;
        }
        //set la man selon les valeurs donnée
        private void setMana(Joueur j, int ble, int bois, int gem)
        {
            j.nbBle = ble;
            j.nbBois = bois;
            j.nbGem = gem;
        }
        //set l'ennemis
        private void setPartie()
        {
            int numEnnemis = Serveur.findEnnemis(posClient, posPartie);
            if (numEnnemis != -1)
                Ennemis = Serveur.getJoueur(numEnnemis);
            else
                Ennemis = null;
            Thread.Sleep(1000);
        }
        //vérifie si le joueur est présent dans la BD
        private bool estPresent(string[] data)
        {
            Serveur.mutPartie1.WaitOne();
            if (acces.estPresent(data[0], data[1]) && !estConnecter(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                Serveur.mutPartie1.ReleaseMutex();
                return true;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                Serveur.mutPartie1.ReleaseMutex();
                return false;
            }
        }
        //vérifie si le joueur est déjà connecter
        private bool estConnecter(string alias)
        {
            for (int i = 0; i < Serveur.tabJoueur.Count; ++i)
            {
                if (Serveur.tabJoueur[i] != null && Serveur.tabJoueur[i].nom == alias)
                    return true;
            }
            return false;
        }
        //vérifie si le joueur peut etre ajouter
        private bool peutEtreAjouter(string[] data)
        {
            Serveur.mutPartie1.WaitOne();
            if (acces.estDejaPresent(data[0]))
            {
                sendClient(Moi.sckJoueur, "oui");
                Serveur.mutPartie1.ReleaseMutex();
                return false;
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
                acces.ajouter(data[0], data[1], data[2], data[3]);
                Serveur.mutPartie1.ReleaseMutex();
                return true;

            }
        }
        //envoie le deck voulue au joueur
        private void sendDeckJoueurClient()
        {
            envoyerDeck(Moi.sckJoueur, monDeck);
        }
        //recois un string du socket
        private string recevoirResultat(Socket client)
        {
            string strData = "";
            bool Arecu = false;
            int atemp = 0;
            while (atemp < 2 && !Arecu)
            {
                try
                {
                    byte[] buff = new byte[client.SendBufferSize];
                    int bytesRead = client.Receive(buff);
                    byte[] formatted = new byte[bytesRead];
                    for (int i = 0; i < bytesRead; i++)
                    {
                        formatted[i] = buff[i];
                    }
                    strData = Encoding.ASCII.GetString(formatted);
                    Arecu = true;
                }
                //ne fait rien car le client n'a rien envoyer parcequ'il c'est déconnecter
                catch (SocketException)
                {
                    if (atemp == 2)
                    {
                        Serveur.mutex.WaitOne();
                        Console.Write("Erreur de telechargement des donnees");
                        Serveur.tabJoueur.Remove(Moi);
                        Deconnection = true;
                        Serveur.mutex.ReleaseMutex();
                    }
                    else
                    {
                        atemp++;
                    }
                }
            }
            return strData;
        }
        //envoie un string au serveur
        private void sendClient(Socket client, String text)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(text);
                client.Send(data);
                Thread.Sleep(150);
            }
            catch
            {
                Serveur.mutex.WaitOne();
                Console.Write("Erreur de telechargement des donnees");
                Serveur.tabJoueur.Remove(Moi);
                Deconnection = true;
                Serveur.mutex.ReleaseMutex();
            }
        }
        //envoie les profils
        private void sendProfil(string alias)
        {
            Serveur.mutPartie1.WaitOne();
            string profile = acces.getProfil(alias);
            if (profile != null)
            {
                sendClient(Moi.sckJoueur, profile);
            }
            else
            {
                sendClient(Moi.sckJoueur, "non");
            }
            Serveur.mutPartie1.ReleaseMutex();
        }
        //vérifie si le joueur est mort
        private bool aPerdu(Joueur player)
        {
            if (player != null && player.vie <= 0)
            {
                return true;
            }
            return false;
        }
        //envoie tout les nom de deck au joueur
        private void sendDeck(string alias)
        {
            string deck = acces.getDeckJoueur(alias);
            if (deck != "")
                sendClient(Moi.sckJoueur, deck);
            else
                sendClient(Moi.sckJoueur, "aucun deck");
        }

    }
}

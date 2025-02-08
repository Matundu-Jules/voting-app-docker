# Application de vote - Guide d'Installation et d'Utilisation avec Docker

## Installation requise : 
Avant de commencer, assurez-vous d’avoir les outils suivants installés sur votre machine :
- **Docker**

## Cloner le projet :
Ouvrez un terminal et exécutez la commande suivante :

```bash
git clone https://github.com/Matundu-Jules/voting-app-docker.git
cd voting-app-docker
```

## Lancement de l’application avec Docker Compose :

1. Assurez-vous d’être dans le dossier du projet.
2. Exécutez la commande suivante pour construire et démarrer les conteneurs :

```bash
docker-compose up --build -d
```

⚠️ Remarque : L’option -d permet de démarrer les conteneurs en arrière-plan.

## Accès aux services :
Une fois l’application démarrée, vous pouvez accéder aux interfaces suivantes :

**Vote** (soumettre un vote) : http://localhost:8080

**Résultats** (voir les résultats en temps réel) : http://localhost:5000


Vérifiez que les services sont bien en cours d’exécution avec :
```bash
docker ps
```


## Arrêt & nettoyage :

Pour arrêter et supprimer les conteneurs, exécutez :

```bash
docker-compose down
```

Si vous souhaitez supprimer les volumes associés (⚠️ perte des données), utilisez :

```bash
docker-compose down -v
```

🚀 Bonne utilisation !
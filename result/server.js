const express = require('express');
const { Pool } = require('pg');
const path = require('path');
const cookieParser = require('cookie-parser');
const bodyParser = require('body-parser');
const methodOverride = require('method-override');
const http = require('http');
const socketIo = require('socket.io');

const app = express();
const server = http.createServer(app);
const io = socketIo(server, { transports: ['polling'] });

io.set('transports', ['polling']);

const PORT = process.env.NODE_PORT;

const dbConfig = {
  user: process.env.POSTGRES_USER,
  host: process.env.POSTGRES_HOST,
  database: process.env.POSTGRES_DB,
  password: process.env.POSTGRES_PASSWORD,
  port: process.env.POSTGRES_PORT,
  max: 10, // Limite de connexions simultanées
  idleTimeoutMillis: 30000, // Timeout d'inactivité
  connectionTimeoutMillis: 5000, // Timeout de connexion
};

let pool;

// Fonction de connexion avec gestion des erreurs
async function connectDB(retries = 5, delay = 2000) {
  while (retries > 0) {
    try {
      pool = new Pool(dbConfig);
      await pool.query('SELECT 1');
      console.log('✅ Connected to db');
      return;
    } catch (err) {
      console.error(
        `❌ Database connection failed. Retrying in ${delay / 1000}s... (${retries} attempts left)`
      );
      retries--;
      await new Promise((res) => setTimeout(res, delay));
      delay *= 2; // Augmente le délai à chaque tentative
    }
  }
  console.error('🚨 Could not connect to the database. Exiting.');
  process.exit(1);
}

// Gestion des connexions Socket.io
io.on('connection', (socket) => {
  socket.emit('message', { text: 'Welcome!' });

  socket.on('subscribe', (data) => {
    socket.join(data.channel);
  });
});

// Attendre la connexion à la base avant de récupérer les votes
async function getVotes() {
  if (!pool) {
    console.error('❌ Database connection not available.');
    return;
  }

  try {
    const result = await pool.query(
      'SELECT vote, COUNT(id) AS count FROM votes GROUP BY vote'
    );
    const votes = collectVotesFromResult(result);
    io.sockets.emit('scores', JSON.stringify(votes));
  } catch (err) {
    onsole.error('⚠️ Error performing query:', err.message);
  } finally {
    setTimeout(getVotes, 1000);
  }
}

// Fonction de formatage des votes
function collectVotesFromResult(result) {
  const votes = { a: 0, b: 0 };

  result.rows.forEach((row) => {
    votes[row.vote] = parseInt(row.count);
  });

  return votes;
}

// Middleware
app.use(cookieParser());
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));
app.use(methodOverride('X-HTTP-Method-Override'));
app.use((req, res, next) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header(
    'Access-Control-Allow-Headers',
    'Origin, X-Requested-With, Content-Type, Accept'
  );
  res.header('Access-Control-Allow-Methods', 'PUT, GET, POST, DELETE, OPTIONS');
  next();
});

app.use(express.static(__dirname + '/views'));

app.get('/', (req, res) => {
  res.sendFile(path.resolve(__dirname + '/views/index.html'));
});

// Démarrage du serveur
server.listen(PORT, async () => {
  console.log(`🚀 App running on port ${PORT}`);
  await connectDB(); // Lancer la connexion à PostgreSQL
  getVotes(); // Démarrer la récupération des votes après la connexion DB
});

from flask import Flask, render_template, request, make_response
from redis import Redis
import os
import socket
import random
import json
import logging

option_a = os.getenv('OPTION_A', "Cats")
option_b = os.getenv('OPTION_B', "Dogs")
hostname = socket.gethostname()

redis_host = os.getenv("REDIS_HOST")  # Connexion Redis
redis_port = int(os.getenv("REDIS_PORT"))
redis_password = os.getenv("REDIS_PASSWORD")

redis_url = f"redis://:{redis_password}@{redis_host}:{redis_port}"

flask_host = os.getenv("FLASK_RUN_HOST")  # Hôte Flask
flask_port = int(os.getenv("FLASK_RUN_PORT"))  # Port Flask
flask_debug = os.getenv("FLASK_DEBUG").lower() == "true"  # Mode Debug

app = Flask(__name__)
app.logger.setLevel(logging.INFO)

def get_redis():
    if not hasattr(Flask, 'redis'):
        Flask.redis = Redis.from_url(redis_url, decode_responses=True)
    return Flask.redis

@app.route("/", methods=['POST', 'GET'])
def hello():
    voter_id = request.cookies.get('voter_id')
    if not voter_id:
        voter_id = hex(random.getrandbits(64))[2:-1]

    vote = None

    if request.method == 'POST':
        redis = get_redis()
        vote = request.form['vote']
        app.logger.info('Received vote for %s', vote)
        data = json.dumps({'voter_id': voter_id, 'vote': vote})
        redis.rpush('votes', data)

    resp = make_response(render_template(
        'index.html',
        option_a=option_a,
        option_b=option_b,
        hostname=hostname,
        vote=vote,
    ))
    resp.set_cookie('voter_id', voter_id)
    return resp

if __name__ == "__main__":
    app.run(host=flask_host, port=flask_port, debug=flask_debug, threaded=True)

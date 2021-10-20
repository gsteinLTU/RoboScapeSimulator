const Matter = require('matter-js');
const Engine = Matter.Engine,
    World = Matter.World,
    Bodies = Matter.Bodies,
    Events = Matter.Events;
const _ = require('lodash');
const shortid = require('shortid');
const fs = require('fs');
const path = require('path');
const PNG = require('pngjs').PNG;

const defaultSettings = {
    robotKeepAliveTime: 1000 * 60 * 10,
    fps: 60,
    environment: 'default'
};

Matter.Resolver._restingThresh = 0.1;
const SENSORS = (process.env.SENSORS || 'parallax').toLowerCase();

class Room {
    constructor(settings = {}) {
        // Get unique ID for this Room
        if(settings.roomID == undefined) {
            this.roomID = shortid.generate();

            while (Room.existingIDs.indexOf(this.roomID) !== -1) {
                this.roomID = shortid.generate();
            }
        } else {
            this.roomID = settings.roomID;
        }

        this.robots = [];

        this.debug = require('debug')(`roboscape-sim:Room-${this.roomID}`);
        this.debug('Creating room');

        this.settings = _.defaults(settings, defaultSettings);

        this.engine = Engine.create();
        this.engine.world.gravity.y = 0;

        // Load environment objects
        this.setupEnvironment(this.settings.environment);

        // Begin update loop
        this.updateInterval = setInterval(
            function () {
                Engine.update(this.engine, 1000 / this.settings.fps);
            }.bind(this),
            1000 / this.settings.fps
        );
    }

    /**
     * Add initial objects to room
     * @param {String} environment Filename of environment to use
     */
    setupEnvironment(environment = 'default_box') {
        Room.listEnvironments().then(list => {
            // Validate that file actually exists on server as an environment
            if(list.map(env => env.file).indexOf(environment) === -1) {
                this.debug('Invalid environment requested, using default');
                environment = 'default';
            }

            // Load environment info from file
            fs.readFile(path.join(__dirname, '..', 'environments', environment + '.json'), (err, data) => {
                if (err) {
                    this.debug(`Error loading environment ${environment}`);
                    return;
                }

                let parsed = JSON.parse(data);
                this.debug(`Loading environment ${parsed.name}...`);

                for (let object of parsed.objects) {
                    var body = Bodies.rectangle(object.x, object.y, object.width, object.height, { label: object.label, isStatic: object.isStatic || false, frictionAir: object.frictionAir || 0.7 });
                    body.width = object.width;
                    body.height = object.height;
                    body.image = object.image;

                    World.add(this.engine.world, body);
                }

                // Get spawn settings
                if (parsed.robotSpawn.spawnType == 'RandomPosition') {
                    this.settings.robotSpawnType = 'RandomPosition';
                    this.settings.minX = parsed.robotSpawn.minX;
                    this.settings.maxX = parsed.robotSpawn.maxX;
                    this.settings.minY = parsed.robotSpawn.minY;
                    this.settings.maxY = parsed.robotSpawn.maxY;
                    this.settings.robotTypes = parsed.robotSpawn.robotTypes
                        .filter(type => {
                            return Object.keys(Room.robotTypes).indexOf(type) !== -1;
                        })
                        .map(type => Room.robotTypes[type]);

                    if (parsed.background != undefined) {
                        this.settings.background = parsed.background.image || '';

                        // Load background 
                        this.backgroundImage = fs.createReadStream(path.join(__dirname, '..', 'public', 'img', 'backgrounds', this.settings.background + '.png'))
                            .pipe(new PNG());
                    } else {
                        this.settings.background = '';
                        this.backgroundImage = null;
                    }
                }
            });

            const handleCollisionEvent = function (type, event) {
                var pairs = event.pairs;
                for (var i = 0; i < pairs.length; i++) {
                    var pair = pairs[i];
                    if (pair.bodyA[type] !== undefined) {
                        pair.bodyA[type]();
                    }
                    if (pair.bodyB[type] !== undefined) {
                        pair.bodyB[type]();
                    }
                }
            };

            // Setup collision events
            Events.on(this.engine, 'collisionStart', handleCollisionEvent.bind(this, 'onCollisionStart'));
            Events.on(this.engine, 'collisionEnd', handleCollisionEvent.bind(this, 'onCollisionEnd'));
        });
    }

    /**
     * Returns an array of the objects in the scene
     */
    getBodies(onlySleeping = true, allData = false) {
        let relevantBodies = this.engine.world.bodies.filter(body => !onlySleeping || (!body.isSleeping && !body.isStatic));

        if (allData) {
            return relevantBodies.map(body => {
                let bodyInfo = {
                    label: body.label,
                    pos: body.position,
                    vel: body.velocity,
                    angle: body.angle,
                    anglevel: body.angularVelocity,
                    width: body.width,
                    height: body.height,
                    image: body.image
                };

                // Add LED status if it exists
                if (body.ledStatus !== undefined) {
                    bodyInfo.ledStatus = body.ledStatus;
                }

                return bodyInfo;
            });
        } else {
            return relevantBodies.map(body => {
                // Only position/orientation for update
                let bodyInfo = { label: body.label, pos: body.position, angle: body.angle, vel: body.velocity, anglevel: body.angularVelocity };

                // Add LED status if it exists
                if (body.ledStatus !== undefined) {
                    bodyInfo.ledStatus = body.ledStatus;
                }

                return bodyInfo;
            });
        }
    }

    /**
     * Add a robot to the room
     * @param {String} mac
     * @param {Matter.Vector} position
     * @returns {Robot} Robot created
     */
    addRobot(mac = null, position = null) {
        // Use loaded spawn type
        let settings = null;
        if (position === null && this.settings.robotSpawnType === 'RandomPosition') {
            settings = {
                minX: this.settings.minX,
                maxX: this.settings.maxX,
                minY: this.settings.minY,
                maxY: this.settings.maxY
            };
        }

        // Create a robot of a random allowable type
        let robotType = Math.floor(Math.random() * this.settings.robotTypes.length);
        let bot = new this.settings.robotTypes[robotType](mac, position, this.engine, settings);

        this.robots.push(bot);
        bot.room = this;

        this.debug(`Robot ${bot.mac} added to room`);

        return bot;
    }

    /**
     * Removes robots that have not received a command recently
     * @returns {Boolean} Whether robots were removed
     */
    removeDeadRobots() {
        let deadRobots = this.robots.filter(robot => {
            return this.settings.robotKeepAliveTime > 0 && Date.now() - robot.lastCommandTime > this.settings.robotKeepAliveTime;
        });

        if (deadRobots.length > 0) {
            this.debug(
                'Dead robots: ',
                deadRobots.map(robot => robot.mac)
            );
            this.robots = _.without(this.robots, ...deadRobots);

            // Cleanup
            deadRobots.forEach(robot => {
                robot.close();
                robot.body.parts.forEach(World.remove.bind(this, this.engine.world));
            });

            return true;
        }

        return false;
    }

    /**
     * Destroy this room
     */
    close() {
        this.debug('Closing room...');

        this.robots.forEach(robot => {
            robot.close();
        });

        clearInterval(this.updateInterval);
    }

    /**
     * Returns an array of environments usable in Rooms
     */
    static listEnvironments() {
        return new Promise(resolve => {
            fs.readdir(path.join(__dirname, '..', 'environments'), (err, files) => {
                let environments = [];
                if (err) {
                    require('debug')('roboscape-sim:Room')('Error loading environments');
                    return;
                }

                for (let file of files) {
                    let fileData = fs.readFileSync(path.join(__dirname, '..', 'environments', file));
                    let parsed = JSON.parse(fileData);

                    // Check for unsupported sensor types
                    if (parsed.robotSpawn.requiredExtraSensors == undefined
                        || parsed.robotSpawn.requiredExtraSensors.length === 0
                        || SENSORS === 'all'){
                        environments.push({
                            file: path.basename(file, '.json'),
                            name: parsed.name
                        });
                    }
                }

                resolve(environments);
            });
        });
    }

    /**
     * Handle an event from a browser client in this room
     * @param {String} type 
     * @param {Object} data 
     * @param {SocketIO.Socket} socket 
     */
    // eslint-disable-next-line no-unused-vars
    onClientEvent(type, data, socket) {
        let temp;
        switch(type){
        case 'parallax_hw_button':
            // Create Button message
            temp = Buffer.alloc(2);
            temp.write('P');
            temp.writeUInt8(data.status ? 0 : 1, 1);

            // Send button state to server
            this.robots.find(r => r.mac == data.mac).sendToServer(temp);
            break;
        default:
            this.debug(`Unknown client event: ${type}`);
        }
    }

    /**
     * Send a client the information about the room
     * @param {SocketIO.Socket} socket 
     */
    sendRoomInfo(socket) {
        let info = {};

        info.background = this.settings.background;

        socket.emit('roomInfo', info);
    }
}

Room.existingIDs = [];

/**
 * List of available robot types
 */
Room.robotTypes = {
    ParallaxRobot: require('./robots/ParallaxRobot'),
    ParallaxRobotLidar: require('./robots/ParallaxRobotLidar'),
    ParallaxRobotLight: require('./robots/ParallaxRobotLight'),
    OmniRobot: require('./robots/OmniRobot')
};

module.exports = Room;

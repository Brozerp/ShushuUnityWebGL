#include <WiFi.h>
#include <WebServer.h>
#include <WebSocketsServer.h>

// Configuration du point d'accès
const char* ssid = "ESP32_Network"; // Nom du réseau WiFi
const char* password = "12345678";  // Mot de passe pour le réseau

// Adresse IP fixe pour le point d'accès
IPAddress local_IP(192, 168, 4, 1); // Adresse IP du point d'accès
IPAddress gateway(192, 168, 4, 1);   // Passerelle
IPAddress subnet(255, 255, 255, 0);  // Masque de sous-réseau

// Serveur WebSocket
WebSocketsServer webSocket = WebSocketsServer(8600);

// Serveur HTTP
WebServer httpServer(80);

// Définition des broches et variables
const int buttonPins[] = {21, 4, 5, 15, 16, 17, 18, 19};
const char* buttonNames[] = {"B|21", "B|4", "B|5", "B|15", "B|16", "B|17", "B|18", "B|19"};
const int buttonCount = sizeof(buttonPins) / sizeof(buttonPins[0]);

int lastPotValue = -1;
int lastJoystickXValue = -1;
int lastJoystickYValue = -1;
bool lastJoystickButtonState = false; // État précédent du bouton du joystick
bool buttonStates[8] = {0};

const int potPin = 34;
const int joystickXPin = 36;
const int joystickYPin = 39;
const int joystickButtonPin = 3;
const int threshold = 220;
const int joystickThreshold = 120; // Seuil pour les valeurs du joystick

void setup() {
  Serial.begin(115200);

  // Démarrage du point d'accès WiFi avec IP fixe
  if (!WiFi.softAPConfig(local_IP, gateway, subnet)) {
    Serial.println("Erreur de configuration de l'IP !");
  }
  WiFi.softAP(ssid, password);
  
  Serial.println("Point d'accès démarré");
  Serial.print("IP du point d'accès : ");
  Serial.println(WiFi.softAPIP());

  // Initialisation du serveur WebSocket
  webSocket.begin();
  webSocket.onEvent(webSocketEvent);

  // Initialisation des broches des boutons
  for (int i = 0; i < buttonCount; i++) {
    pinMode(buttonPins[i], INPUT_PULLUP);
  }
  pinMode(joystickButtonPin, INPUT_PULLUP);

  // Initialisation du serveur HTTP
  setupHttpServer();
}

void loop() {
  delayMicroseconds(50);
  webSocket.loop();
  httpServer.handleClient();
  // Vérification des boutons et envoi des données
  for (int i = 0; i < buttonCount; i++) {
    bool isPressed = (digitalRead(buttonPins[i]) == LOW);
    if (isPressed && !buttonStates[i]) {
      buttonStates[i] = true;
      String message = String(buttonNames[i]);
      webSocket.broadcastTXT(message);
    } else if (!isPressed && buttonStates[i]) {
      buttonStates[i] = false;
    }
  }

  // Lecture des autres capteurs
  sendSensorData();

  // Vérification du bouton du joystick
  bool currentJoystickButtonState = (digitalRead(joystickButtonPin) == LOW); // Bouton pressé = LOW
  if (currentJoystickButtonState && !lastJoystickButtonState) {
    lastJoystickButtonState = true;
    String message = "B|3";
    webSocket.broadcastTXT(message);
  } else if (!currentJoystickButtonState && lastJoystickButtonState) {
    lastJoystickButtonState = false;
  }

  // Envoyer la position du joystick à intervalles réguliers
  static unsigned long lastSendTime = 0;
  if (millis() - lastSendTime > 16) { // toutes les 10 ms
    sendJoystickData();
    lastSendTime = millis();
  }
}

void sendSensorData() {
  int potValue = analogRead(potPin);
  if (lastPotValue == -1 || abs(potValue - lastPotValue) >= threshold) {
    lastPotValue = potValue;
    // Normaliser la valeur du potentiomètre entre 0 et 1
    float normalizedPotValue = potValue / 4095.0; 
    String message = "O|" + String(potValue);
    webSocket.broadcastTXT(message);
  }
}

void sendJoystickData() {
  int joystickXValue = analogRead(joystickXPin);
  int joystickYValue = analogRead(joystickYPin);

  // Vérification des changements significatifs dans les valeurs du joystick
    String messageX = "X|" + String(joystickXValue);
    webSocket.broadcastTXT(messageX);
    String messageY = "Y|" + String(joystickYValue);
    webSocket.broadcastTXT(messageY);
}

void webSocketEvent(uint8_t num, WStype_t type, uint8_t * payload, size_t length) {
  switch (type) {
    case WStype_CONNECTED:
      Serial.printf("Client connecté : %u\n", num);
      break;

    case WStype_DISCONNECTED:
      Serial.printf("Client déconnecté : %u\n", num);
      break;

    case WStype_TEXT:
      Serial.printf("Message reçu : %s\n", payload);
      break;
  }
}

void setupHttpServer() {
  httpServer.on("/", HTTP_GET, []() {
    httpServer.send(200, "text/plain", "ESP32 WebSocket Server");
  });

  httpServer.on("/", HTTP_OPTIONS, []() {
    httpServer.sendHeader("Access-Control-Allow-Origin", "*");
    httpServer.sendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    httpServer.sendHeader("Access-Control-Allow-Headers", "Content-Type");
    httpServer.send(204);
  });

  httpServer.onNotFound([]() {
    httpServer.send(404, "text/plain", "Not Found");
  });

  httpServer.begin();
  Serial.println("Serveur HTTP démarré.");
}

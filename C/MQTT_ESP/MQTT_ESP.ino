#include <Arduino.h>
#include <WiFi.h>
#include <DHT.h>
#include <ESP32MQTTClient.h>
#include <math.h>
#include <esp_idf_version.h>

namespace Net {
  constexpr char WIFI_SSID[] = "";
  constexpr char WIFI_PASSWORD[] = "";

  constexpr unsigned long CONNECT_TIMEOUT = 20000;

  void connectWifi() {
    while (WiFi.status() != WL_CONNECTED) {
      Serial.printf("Connecting to Wi-Fi '%s'", WIFI_SSID);

      WiFi.disconnect();
      WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

      unsigned long start = millis();

      while (WiFi.status() != WL_CONNECTED &&
             millis() - start < CONNECT_TIMEOUT) {
        delay(500);
        Serial.print(".");
      }

      if (WiFi.status() != WL_CONNECTED) {
        Serial.println(" failed. Retrying...");
      }
    }

    WiFi.setSleep(false);

    Serial.printf(
      "\nWi-Fi connected. IP: %s\n",
      WiFi.localIP().toString().c_str()
    );
  }

  void initWifi() {
    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(true);
    connectWifi();
  }

  void ensureWifi() {
    if (WiFi.status() != WL_CONNECTED) {
      Serial.println("Wi-Fi disconnected. Reconnecting...");
      connectWifi();
    }
  }
}

namespace Sensor {
  constexpr uint8_t DHT_PIN = 4;
  constexpr uint8_t DHT_TYPE = DHT11;

  DHT dht(DHT_PIN, DHT_TYPE);

  void initSensor() {
    dht.begin();
    delay(1000);
  }

  float readTemperature() {
    return dht.readTemperature();
  }

  float readHumidity() {
    return dht.readHumidity();
  }
}

namespace Mqtt {
  constexpr unsigned long PUBLISH_INTERVAL = 5000;
  constexpr unsigned long RETRY_LOG_INTERVAL = 3000;

  unsigned long lastPublishTime = 0;
  unsigned long lastRetryLog = 0;

  constexpr char MQTT_HOST[] = "";
  constexpr uint16_t MQTT_PORT = 1883;
  constexpr char MQTT_USERNAME[] = "";
  constexpr char MQTT_PASSWORD[] = "";

  constexpr char DEVICE_ID[] = "sensor-01";
  constexpr char MQTT_TOPIC[] = "telemetry/sensor-01";

  ESP32MQTTClient mqttClient;

  void initMqtt() {
    mqttClient.setURL(
      MQTT_HOST,
      MQTT_PORT,
      MQTT_USERNAME,
      MQTT_PASSWORD
    );

    mqttClient.setMqttClientName(DEVICE_ID);
    mqttClient.loopStart();

    Serial.printf(
      "Connecting to MQTT broker %s:%u",
      MQTT_HOST,
      MQTT_PORT
    );

    while (!mqttClient.isConnected()) {
      delay(1000);
      Serial.print(".");

      Net::ensureWifi();
    }

    Serial.println("\nMQTT connected!");
  }

  void ensureMqtt() {
    if (mqttClient.isConnected()) {
      return;
    }

    unsigned long now = millis();

    if (now - lastRetryLog >= RETRY_LOG_INTERVAL) {
      lastRetryLog = now;

      Serial.printf(
        "MQTT disconnected. Waiting for reconnection to %s:%u...\n",
        MQTT_HOST,
        MQTT_PORT
      );
    }

    Net::ensureWifi();
  }

  void publishMqttMessage() {
    if (!mqttClient.isConnected()) {
      return;
    }

    unsigned long currentTime = millis();

    if (currentTime - lastPublishTime < PUBLISH_INTERVAL) {
      return;
    }

    lastPublishTime = currentTime;

    float temperature = Sensor::readTemperature();
    float humidity = Sensor::readHumidity();

    if (isnan(temperature) || isnan(humidity)) {
      Serial.println("Failed to read DHT11 sensor");
      return;
    }

    char payload[128];

    snprintf(
      payload,
      sizeof(payload),
      "{\"deviceId\":\"%s\",\"value\":%.1f,\"unit\":\"C\"}",
      DEVICE_ID,
      temperature
    );

    mqttClient.publish(MQTT_TOPIC, payload, 0, false);
    Serial.printf("%s -> %s\n", MQTT_TOPIC, payload);

    snprintf(
      payload,
      sizeof(payload),
      "{\"deviceId\":\"%s\",\"value\":%.1f,\"unit\":\"%%\"}",
      DEVICE_ID,
      humidity
    );

    mqttClient.publish(MQTT_TOPIC, payload, 0, false);
    Serial.printf("%s -> %s\n", MQTT_TOPIC, payload);
  }
}

void onMqttConnect(esp_mqtt_client_handle_t client) {
  Mqtt::mqttClient.publish(
    "devices/status",
    "online",
    0,
    true
  );
}

#if ESP_IDF_VERSION < ESP_IDF_VERSION_VAL(5, 0, 0)

esp_err_t handleMQTT(esp_mqtt_client_handle_t event) {
  Mqtt::mqttClient.onEventCallBack(event);
  return ESP_OK;
}

#else

void handleMQTT(
  void *handlerArgs,
  esp_event_base_t base,
  int32_t eventId,
  void *eventData
) {
  auto event = static_cast<esp_mqtt_event_handle_t>(eventData);
  Mqtt::mqttClient.onEventCallback(event);
}

#endif

void setup() {
  Serial.begin(115200);
  delay(1000);

  Sensor::initSensor();
  Net::initWifi();
  Mqtt::initMqtt();
}

void loop() {
  Net::ensureWifi();
  Mqtt::ensureMqtt();
  Mqtt::publishMqttMessage();

  delay(10);
}

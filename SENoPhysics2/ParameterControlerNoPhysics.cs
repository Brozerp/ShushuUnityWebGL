using System.Collections;
using UnityEngine;
using Live2D.Cubism.Core;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

public class ParameterController : MonoBehaviour
{
    CubismModel model;

    CubismParameter neutral;
    CubismParameter contrarie;
    CubismParameter blase;
    CubismParameter colere;
    CubismParameter content;
    CubismParameter blink;
    CubismParameter blush;

    CubismParameter ParamEyeSize;
    CubismParameter ParamEyeBallX;
    CubismParameter ParamEyeBallY;

    public float eyeSizeMin = 0f;
    public float eyeSizeMax = 1f;
    public float eyeBallMin = -1f;
    public float eyeBallMax = 1f;

    CubismParameter CurrentEmotion;

    public float blinkDuration = 0.1f;
    public float blinkDurationContent = 0.9f;

    private ClientWebSocket websocket;
    private CancellationTokenSource cancellationTokenSource;

    async void Start()
    {
        model = this.FindCubismModel();
        if (model == null)
        {
            Debug.LogError("Le modèle Cubism n'a pas été trouvé !");
            return;
        }

        neutral = GetParameter("TriggerNeutral");
        contrarie = GetParameter("Param4");
        blase = GetParameter("TriggerBlase");
        colere = GetParameter("Param5");
        content = GetParameter("Param6");
        blink = GetParameter("Wink");
        blush = GetParameter("TriggerBlush");

        ParamEyeSize = GetParameter("EyeSizeParam");
        ParamEyeBallX = GetParameter("ParamEyeBallY");
        ParamEyeBallY = GetParameter("ParamEyeBallX");

        ActivateEmotion(neutral);

        websocket = new ClientWebSocket();
        cancellationTokenSource = new CancellationTokenSource();
        await ConnectWebSocket();
    }

    void Update()
    {
        // Aucune logique directe dans Update pour WebSocket
    }

    async Task ConnectWebSocket()
    {
        try
        {
            Debug.Log("Tentative de connexion au WebSocket...");
            await websocket.ConnectAsync(new System.Uri("ws://192.168.4.1:81/"), cancellationTokenSource.Token);
            Debug.Log("WebSocket connecté avec succès !");
            _ = ReceiveWebSocketMessages();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Erreur de connexion WebSocket : {ex.Message}");
        }
    }

    async Task ReceiveWebSocketMessages()
    {
        byte[] buffer = new byte[1024];

        while (websocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;

            try
            {
                result = await websocket.ReceiveAsync(buffer, cancellationTokenSource.Token);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Erreur lors de la réception des messages WebSocket : {ex.Message}");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                Debug.Log("WebSocket fermé.");
                break;
            }

            string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Debug.Log($"Message reçu : {message}");
            UpdateParameters(message);
        }
    }

    public void UpdateEyeballSize(float value)
    {
        if (ParamEyeSize != null)
        {
            ParamEyeSize.Value = Mathf.Clamp(value, eyeSizeMin, eyeSizeMax);
        }
    }

    public void UpdateEyeBallX(float value)
    {
        if (ParamEyeBallX != null)
        {
            ParamEyeBallX.Value = Mathf.Clamp(value, eyeBallMin, eyeBallMax);
        }
    }

    public void UpdateEyeBallY(float value)
    {
        if (ParamEyeBallY != null)
        {
            ParamEyeBallY.Value = Mathf.Clamp(value, eyeBallMin, eyeBallMax);
        }
    }

    public void UpdateParameters(string data)
    {
        if (data == "B|15")
        {
            blinkDuration = 0.1f;
            ActivateEmotion(neutral);
        }
        else if (data == "B|16")
        {
            blinkDuration = 0.1f;
            ActivateEmotion(contrarie);
        }
        else if (data == "B|17")
        {
            blinkDuration = 0.25f;
            ActivateEmotion(blase);
        }
        else if (data == "B|18")
        {
            blinkDuration = 0.1f;
            ActivateEmotion(colere);
        }
        else if (data == "B|21")
        {
            ActivateEmotion(content);
            blinkDuration = blinkDurationContent;
        }
        else if (data == "B|5")
        {
            ActivateBlink();
        }
        else if (data == "B|4")
        {
            ToggleBlush();
        }
        else if (data.StartsWith("O|"))
        {
            string valueString = data.Substring("O|".Length);
            if (int.TryParse(valueString, out int potValue))
            {
                potValue = Mathf.Clamp(potValue, 0, 4095);
                float eyeSizeValue = potValue / 4095f;
                ParamEyeSize.Value = eyeSizeValue;
                Debug.Log($"O|{eyeSizeValue}");
            }
        }
        else if (data.StartsWith("X|"))
        {
            string valueString = data.Substring("X|".Length);
            if (int.TryParse(valueString, out int joystickXValue))
            {
                joystickXValue = Mathf.Clamp(joystickXValue, 0, 4095);
                float eyeBallYValue = (joystickXValue - 2048) / 2047f;
                ParamEyeBallY.Value = eyeBallYValue;
                Debug.Log($"X|{eyeBallYValue}");
            }
        }
        else if (data.StartsWith("Y|"))
        {
            string valueString = data.Substring("Y|".Length);
            if (int.TryParse(valueString, out int joystickYValue))
            {
                joystickYValue = Mathf.Clamp(joystickYValue, 0, 4095);
                float eyeBallXValue = (joystickYValue - 2048) / 2047f;
                ParamEyeBallX.Value = eyeBallXValue;
                Debug.Log($"Y|{eyeBallXValue}");
            }
        }
    }

    private void ActivateEmotion(CubismParameter emotion)
    {
        if(CurrentEmotion != emotion){
        ResetAllEmotions();
        Debug.Log(emotion + ". PrecedenteEmotion: "+CurrentEmotion+". Nouvelle emotion: "+emotion);
            CurrentEmotion = emotion;
            emotion.Value = 1f;
            StartCoroutine(BlinkOnEmotionChange());
        }
    }

    private IEnumerator BlinkOnEmotionChange()
    {
        if (blink != null)
        {
            blink.Value = 1f;

            float elapsedTime = 0f;

            while (elapsedTime < blinkDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / blinkDuration);
                float easedValue = t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

                blink.Value = Mathf.Lerp(1f, 0f, easedValue);
                yield return null;
            }

            blink.Value = 0f;
        }
    }

    private void ResetAllEmotions()
    {
        SetParameterValue(neutral, 0);
        SetParameterValue(contrarie, 0);
        SetParameterValue(blase, 0);
        SetParameterValue(colere, 0);
        SetParameterValue(content, 0);
    }

    private void SetParameterValue(CubismParameter parameter, float value)
    {
        if (parameter != null)
        {
            parameter.Value = value;
        }
    }

    private void ToggleBlush()
    {
        if (blush != null)
        {
            blush.Value = blush.Value == 0 ? 1 : 0;
        }
    }

    private void ActivateBlink()
    {
        if (blink != null)
        {
            StartCoroutine(AnimateBlink());
        }
    }

    private IEnumerator AnimateBlink()
    {
        if (blink != null)
        {
            float targetValue = 1f;
            float currentValue = blink.Value;

            float elapsedTime = 0f;

            while (elapsedTime < blinkDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / blinkDuration;
                t = Mathf.Clamp01(t);
                float easedValue = t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

                blink.Value = Mathf.Lerp(currentValue, targetValue, easedValue);
                yield return null;
            }

            elapsedTime = 0f;
            while (elapsedTime < blinkDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / blinkDuration;
                t = Mathf.Clamp01(t);
                float easedValue = t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

                blink.Value = Mathf.Lerp(targetValue, 0f, easedValue);
                yield return null;
            }

            blink.Value = 0f;
        }
    }

    private CubismParameter GetParameter(string id)
    {
        var parameter = model.Parameters.FindById(id);
        if (parameter == null)
        {
            Debug.LogWarning($"Le paramètre '{id}' n'a pas été trouvé !");
        }
        return parameter;
    }
}

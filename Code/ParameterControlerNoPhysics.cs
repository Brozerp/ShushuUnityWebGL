using System.Collections;
using UnityEngine;
using Live2D.Cubism.Core;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public class ParameterController : MonoBehaviour
{

    [DllImport("__Internal")]
    private static extern void InitializeWebSocket();

    [DllImport("__Internal")]
    private static extern string GetLastMessage();


    private Dictionary<CubismParameter, float> currentValues = new Dictionary<CubismParameter, float>();
    private Dictionary<CubismParameter, float> targettingValues = new Dictionary<CubismParameter, float>();
    
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

    public float blinkDuration = 0.1f;
    public float blinkDurationContent = 0.5f;

    public float moveNoise = 0.001f;


    private ClientWebSocket websocket;

    async void Start()
    {
        InitializeWebSocket();
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
    }

    void Update(){
        string message =  GetLastMessage();
        if(!string.IsNullOrEmpty(message))
            UpdateParameters(message);

        List<CubismParameter> deletedKeys = new();
        foreach(KeyValuePair<CubismParameter, float> val in targettingValues){
            float newValue;
            if(currentValues.ContainsKey(val.Key)){
                newValue = Mathf.Lerp(currentValues[val.Key], val.Value, Time.deltaTime * 10f);
            }
            else{
                newValue = Mathf.Lerp(val.Key.Value, val.Value, Time.deltaTime * 10f);
            }

            val.Key.Value = newValue;
            currentValues[val.Key] = newValue;
            if(Mathf.Abs(newValue - val.Value) < 0.001f){
                deletedKeys.Add(val.Key);
            }
        }

        foreach(CubismParameter p in deletedKeys){
            targettingValues.Remove(p);
        }

        model.ForceUpdateNow();
    }
    public void UpdateParameters(string data)
    {
        if (data == "B|21")
        {
            Debug.Log($"{data}");
            ResetAllEmotions();
            neutral.Value = 1f;
        }
        else if (data == "B|4")
        {
            Debug.Log($"{data}");
            ResetAllEmotions();
            contrarie.Value = 1f;
        }
        else if (data == "B|5")
        {
            Debug.Log($"{data}");
            ActivateEmotion(blase);
        }
        else if (data == "B|21")
        {
            Debug.Log($"{data}");
            ActivateEmotion(colere);
        }
        else if (data == "B|16")
        {
            Debug.Log($"{data}");
            ActivateEmotion(content);
            blinkDuration = blinkDurationContent;
        }
        else if (data == "B|17")
        {
            Debug.Log($"{data}");
            ActivateBlink();
        }
        else if (data == "B|18")
        {
            Debug.Log($"{data}");
            ToggleBlush();
        }
        else if (data.StartsWith("O|"))
        {
            targettingValues[ParamEyeSize] = float.Parse(data.Substring(2));
            Debug.Log("Set Size to " + targettingValues[ParamEyeSize]);
        }
        else if (data.StartsWith("X|"))
        {
            float value = Mathf.Clamp((float.Parse(data.Substring(2)) - 2047f) / 2047f, eyeBallMin, eyeBallMax);
            if (!targettingValues.ContainsKey(ParamEyeBallY) || Mathf.Abs(targettingValues[ParamEyeBallY] - value) > moveNoise)
            {
                targettingValues[ParamEyeBallY] = value;
                Debug.Log("Set Y to " + targettingValues[ParamEyeBallY]);
            }
        }
        else if (data.StartsWith("Y|"))
        {
            float value = Mathf.Clamp((float.Parse(data.Substring(2)) - 2047f) / 2047f, eyeBallMin, eyeBallMax);
            if(!targettingValues.ContainsKey(ParamEyeBallX) || Mathf.Abs(targettingValues[ParamEyeBallX] - value) > moveNoise)
            {
                targettingValues[ParamEyeBallX] = value;
                Debug.Log("Set X to " + targettingValues[ParamEyeBallX]);
            }
        }
    }

    private void ActivateEmotion(CubismParameter emotion)
    {
        ResetAllEmotions();
        if (emotion != null)
        {
            Debug.Log($"Y|{emotion} activated");
            targettingValues[emotion] = 1f;
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
            targettingValues[parameter] = value;
            //parameter.Value = value;
        }
    }

    private void ToggleBlush()
    {
        if (blush != null)
        {
            targettingValues[blush] = blush.Value == 0 ? 1 : 0;
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



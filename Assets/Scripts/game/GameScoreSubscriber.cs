using UnityEngine;
using System;
using Newtonsoft.Json;

[Serializable]
public class GameScoreUpdate
{
    public string @event;
    public string detected;
    public string led_correct;
    public bool gate_correct;
    public int landings;
    public bool game_complete;
    public string elapsed_seconds;
    public string[] summary;

    // Authoritative server result, relayed onto /game/score_update after submit.
    // On live updates these are default (final == false); on the post-submit
    // message final == true and total carries the leaderboard score.
    public bool final;
    public double total;
    public double objective;
    public double speed_bonus;
}

public class GameScoreSubscriber : MonoBehaviour, IROSSubscriber
{
    [SerializeField]
    private string baseTopicPath = "/game/score_update";

    [SerializeField]
    private string messageType = "std_msgs/msg/String";

    private string _namespacedTopicPath;

    public Action<GameScoreUpdate> OnScoreUpdateReceived;

    public string TopicPath
    {
        get
        {
            if (_namespacedTopicPath == null)
                _namespacedTopicPath = ROSBridgeManager.Instance.ApplyNamespace(baseTopicPath);
            return _namespacedTopicPath;
        }
    }

    public string MessageType => messageType;

    private void OnEnable()
    {
        ROSBridgeManager.Instance.RegisterSubscriber(this);
    }

    private void OnDisable()
    {
        ROSBridgeManager.Instance.UnregisterSubscriber(this);
    }

    public void OnMessageReceived(string message)
    {
        try
        {
            // Message is std_msgs/String, so extract the data field first
            var stringMsg = JsonConvert.DeserializeObject<StdStringMsg>(message);
            if (stringMsg == null || string.IsNullOrEmpty(stringMsg.data)) return;

            var score = JsonConvert.DeserializeObject<GameScoreUpdate>(stringMsg.data);
            if (score != null)
            {
                OnScoreUpdateReceived?.Invoke(score);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing game score update: {e.Message}");
        }
    }

    public void OnSubscribed()
    {
        Debug.Log($"Subscribed to {TopicPath}");
    }

    public void OnDisconnected()
    {
        Debug.Log($"Disconnected from {TopicPath}");
    }

    [Serializable]
    private class StdStringMsg
    {
        public string data;
    }
}

using System;

/// <summary>
/// Interface for any component that wants to subscribe to ROS topics
/// </summary>
public interface IROSSubscriber
{
    /// <summary>
    /// The ROS topic path to subscribe to
    /// </summary>
    string TopicPath { get; }

    /// <summary>
    /// The ROS message type (e.g., "px4_msgs/msg/VehicleOdometry")
    /// </summary>
    string MessageType { get; }

    /// <summary>
    /// Called when a message is received for this subscriber's topic
    /// </summary>
    /// <param name="message">The JSON message data</param>
    void OnMessageReceived(string message);

    /// <summary>
    /// Called when successfully subscribed to the topic
    /// </summary>
    void OnSubscribed();

    /// <summary>
    /// Called when connection to ROS is lost
    /// </summary>
    void OnDisconnected();
}

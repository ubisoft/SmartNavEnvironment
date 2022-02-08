public interface ISmartNavMotion
{
    void ResetState();

    bool IsOnGround();
    
    void CollectObservations(Unity.MLAgents.Sensors.VectorSensor sensor);
    
    void MoveAgent(float[] act);
    
    void OnJumpPadEnter(LaForge.MapGenerator.JumpPad jumpPad);
}

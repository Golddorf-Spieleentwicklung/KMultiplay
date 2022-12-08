using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KMultiplay_Settings
{
    public const int Timeout = 5000; //ms
    public const int MaxPing = 500; //ms
    public const int MaxResendTries = 20; //count
    public const int MaxFileSize = 400;
    public const int PacketsPerSecond = 1024;
    public int MaxPlayers = 8; //Count
    public const int SocketBuffer = 16384;
}

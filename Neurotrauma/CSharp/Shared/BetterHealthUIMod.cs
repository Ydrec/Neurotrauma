using System;
using Barotrauma;
using Barotrauma.Networking;

namespace BetterHealthUI {
    partial class BetterHealthUIMod : ACsMod {
        public static bool IsCampaign => GameMain.GameSession?.GameMode is MultiPlayerCampaign;
        public static bool IsRunning => GameMain.GameSession?.IsRunning ?? false;

        public BetterHealthUIMod() {
            LuaCsSetup.PrintCsMessage("BetterHealthUIMod..ctor");
            #if CLIENT
                InitClient();
            #endif
        }

        //public override void Start() { }
        public override void Stop() { }
    }
}
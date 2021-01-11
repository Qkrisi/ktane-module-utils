using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(KMGameInfo))]
public abstract class KtaneModule : MonoBehaviour
{
    #region General
    protected class ModuleException : Exception
    {
        public ModuleException(string message) : base(message)
        {
        }
    }
    
    private Type ModuleType;
    private Component BossModule;
    private object SceneManager;
    
    private const BindingFlags MainFlags = BindingFlags.Public | BindingFlags.Instance;

    protected KMBombInfo BombInfo;

    protected virtual void Awake()
    {
        ModuleType = GetType();
        if (!DefaultCoroutineQueue.ContainsKey(ModuleType)) DefaultCoroutineQueue.Add(
            ModuleType, 
            new Dictionary<string, CoroutineQueue>()
            {
                {"default", new CoroutineQueue(this)}
            });
    }

    protected virtual void Start()
    {
        IsTestHarness = Application.isEditor;
        
        GetComponent<KMGameInfo>().OnStateChange += state =>
        {
            foreach (var queueDict in DefaultCoroutineQueue)
            {
                foreach (var queue in queueDict.Value) queue.Value.Reset();
            }
            DefaultCoroutineQueue.Clear();
        };
        
        BombInfo = GetComponent<KMBombInfo>();
        BossModule = GetComponent("KMBossModule");
        if(BossModule!=null) IgnoredMethod = BossModule
            .GetType()
            .GetMethod(
                "GetIgnoredModules",
                MainFlags,
                Type.DefaultBinder,
                new Type[] {typeof(string), typeof(string[])}, 
                null
            );
        
        if (!Application.isEditor)
        {
            Type SceneManagerType = ReflectionHelper.FindType("SceneManager", "Assembly-CSharp");
            SceneManager = SceneManagerType
                .GetProperty("Instance", MainFlags | BindingFlags.Static)
                .GetValue(null, null);
            object GameplayStateObject = SceneManagerType
                .GetProperty("GameplayState", MainFlags)
                .GetValue(SceneManager, null);
            MissionObject = GameplayStateObject.GetType().GetField("Mission", MainFlags).GetValue(GameplayStateObject);
            MissionName = (string) MissionObject.GetType().GetField("DisplayNameTerm", MainFlags)
                .GetValue(MissionObject);
            
            Type IRCType = ReflectionHelper.FindType("IRCConnection", "TwitchPlaysAssembly");
            if (IRCType != null)
                SendMethod = IRCType.GetMethod(
                    "SendMessage",
                    MainFlags | BindingFlags.Static,
                    Type.DefaultBinder,
                    new Type[] {typeof(string)},
                    null);
        }
    }

    protected virtual void Update()
    {
        if (BombInfo != null)
        {
            var solveds = BombInfo.GetSolvedModuleNames();
            if (solveds.Count > OldSolveds.Count)
            {
                var TempSolveds = solveds.ToList();
                foreach (string module in OldSolveds) TempSolveds.Remove(module);
                OldSolveds = solveds;
                foreach (string module in TempSolveds)
                {
                    if (IgnoredModules == null || !IgnoredModules.Contains(module)) OnNewStage(module);
                }
            }
        }
    }

    #endregion
    
    #region GameInformation

    protected bool TwitchPlaysActive;
    protected bool TimeModeActive;
    protected bool ZenModeActive;
    protected bool IsTestHarness;
    
    #endregion
    
    #region Missioninformation

    protected object MissionObject;
    protected string MissionName;
    
    #endregion
    
    #region BossModules
    
    private List<string> OldSolveds = new List<string>();
    private MethodInfo IgnoredMethod;
    
    protected string[] IgnoredModules = null;
    protected Action<string> OnNewStage = ModuleName => { };

    protected string[] GetIgnoredModules(string ModuleName, string[] @default = null)
    {
        if(BossModule==null) throw new ModuleException("Module is not a boss module. (Please attach a KMBossModule script!)");
        if (IgnoredMethod == null)
            throw new ModuleException(
                "The GetIgnoredModules method could not be found. Are you using the correct KMBossModule script?");
        IgnoredModules = (string[]) (IgnoredMethod.Invoke(BossModule, new object[] {ModuleName, @default}));
        return IgnoredModules;
    }

    protected string[] GetIgnoredModules(KMBombModule module, string[] @default = null)
    {
        return GetIgnoredModules(module.ModuleDisplayName, @default);
    }
    #endregion
    
    #region CoroutineQueue
    private static Dictionary<Type, Dictionary<string, CoroutineQueue>> DefaultCoroutineQueue = new Dictionary<Type, Dictionary<string, CoroutineQueue>>();

    protected void QueueRoutines(string id, bool SplitYields, params IEnumerator[] routines)
    {
        if (!DefaultCoroutineQueue[ModuleType].ContainsKey(id)) DefaultCoroutineQueue[ModuleType].Add(id, new CoroutineQueue(this));
        DefaultCoroutineQueue[ModuleType][id].QueueRoutines(SplitYields, routines);
    }

    protected void QueueRoutines(params IEnumerator[] routines)
    {
        QueueRoutines("default", false, routines);
    }

    protected void QueueRoutines(bool SplitYields, params IEnumerator[] routines)
    {
        QueueRoutines("default", SplitYields, routines);
    }

    protected void QueueRoutines(string id, params IEnumerator[] routines)
    {
        QueueRoutines(id, false, routines);
    }
    #endregion
    
    #region TwitchPlays
    private MethodInfo SendMethod = null;
    protected void SendTwitchMessage(string message)
    {
        if (SendMethod == null) return;
        Debug.LogFormat("[{0}] Sending message to Twitch chat: {1}", ModuleType.Name, message); 
        SendMethod.Invoke(null, new object[] {message});
    }
    
    protected void SendTwitchMessageFormat(string message, params object[] args) {
        SendTwitchMessage(String.Format(message, args));
    }
    #endregion
}
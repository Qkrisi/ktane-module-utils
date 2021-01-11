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

    /// <summary>
    /// The KMBombInfo object of a module (if present)
    /// </summary>
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
        
        if (!IsTestHarness)
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

    /// <summary>
    /// Will be true if TwitchPlays is active
    /// </summary>
    protected bool TwitchPlaysActive;
    
    /// <summary>
    /// Will be true if Time Mode is active
    /// </summary>
    protected bool TimeModeActive;
    
    /// <summary>
    /// Will be true if Zen Mode is active
    /// </summary>
    protected bool ZenModeActive;
    
    /// <summary>
    /// Will be true if the module is played through the Unity editor
    /// </summary>
    protected bool IsTestHarness;
    
    #endregion
    
    #region Missioninformation

    /// <summary>
    /// The internal Mission object of the current mission
    /// </summary>
    protected object MissionObject;
    
    /// <summary>
    /// The name of the current mission
    /// </summary>
    protected string MissionName;
    
    #endregion
    
    #region BossModules
    
    private List<string> OldSolveds = new List<string>();
    private MethodInfo IgnoredMethod;
    
    /// <summary>
    /// An array of ignored modules. Updated by GetIgnoredModules()
    /// </summary>
    protected string[] IgnoredModules = null;
    
    /// <summary>
    /// Gets called when a non-ignored module gets solved. The passed string value is the name of the solved module.
    /// </summary>
    protected Action<string> OnNewStage = ModuleName => { };

    /// <summary>
    /// Update the list of ignored modules
    /// </summary>
    /// <param name="ModuleName">Name of the module</param>
    /// <param name="default">Ignored modules if Boss Module Manager isn't active</param>
    /// <returns>Reference to IgnoredModules</returns>
    /// <exception cref="ModuleException">There's no KMBossModule attached</exception>
    protected string[] GetIgnoredModules(string ModuleName, string[] @default = null)
    {
        if(BossModule==null) throw new ModuleException("Module is not a boss module. (Please attach a KMBossModule script!)");
        if (IgnoredMethod == null)
            throw new ModuleException(
                "The GetIgnoredModules method could not be found. Are you using the correct KMBossModule script?");
        IgnoredModules = (string[]) (IgnoredMethod.Invoke(BossModule, new object[] {ModuleName, @default}));
        return IgnoredModules;
    }

    /// <summary>
    /// Updates the list of ignored modules
    /// </summary>
    /// <param name="module">The KMBombModule object of the module</param>
    /// <param name="default">Ignored modules if Boss Module Manager isn't active</param>
    /// <returns>Reference to IgnoredModules</returns>
    /// <exception cref="ModuleException">There's no KMBossModule attached</exception>
    protected string[] GetIgnoredModules(KMBombModule module, string[] @default = null)
    {
        return GetIgnoredModules(module.ModuleDisplayName, @default);
    }
    #endregion
    
    #region CoroutineQueue
    private static Dictionary<Type, Dictionary<string, CoroutineQueue>> DefaultCoroutineQueue = new Dictionary<Type, Dictionary<string, CoroutineQueue>>();

    /// <summary>
    /// Add coroutines to the queue
    /// </summary>
    /// <param name="id">The ID of the queue</param>
    /// <param name="SplitYields">If true, the next part of the coroutine will be added to the end of the queue when the one before ran</param>
    /// <param name="routines">Array of coroutines to enqueue</param>
    protected void QueueRoutines(string id, bool SplitYields, params IEnumerator[] routines)
    {
        if (!DefaultCoroutineQueue[ModuleType].ContainsKey(id)) DefaultCoroutineQueue[ModuleType].Add(id, new CoroutineQueue(this));
        DefaultCoroutineQueue[ModuleType][id].QueueRoutines(SplitYields, routines);
    }

    /// <summary>
    /// Add coroutines to the queue
    /// </summary>
    /// <param name="routines">Array of coroutines to enqueue</param>
    protected void QueueRoutines(params IEnumerator[] routines)
    {
        QueueRoutines("default", false, routines);
    }

    /// <summary>
    /// Add coroutines to the queue
    /// </summary>
    /// <param name="SplitYields">If true, the next part of the coroutine will be added to the end of the queue when the one before ran</param>
    /// <param name="routines">Array of coroutines to enqueue</param>
    protected void QueueRoutines(bool SplitYields, params IEnumerator[] routines)
    {
        QueueRoutines("default", SplitYields, routines);
    }

    /// <summary>
    /// Add coroutines to the queue
    /// </summary>
    /// <param name="id">The ID of the queue</param>
    /// <param name="routines">Array of coroutines to enqueue</param>
    protected void QueueRoutines(string id, params IEnumerator[] routines)
    {
        QueueRoutines(id, false, routines);
    }
    #endregion
    
    #region TwitchPlays
    private MethodInfo SendMethod = null;
    
    /// <summary>
    /// Send a message to the Twitch chat
    /// </summary>
    /// <param name="message">The message to send</param>
    protected void SendTwitchMessage(string message)
    {
        if (SendMethod == null) return;
        Debug.LogFormat("[{0}] Sending message to Twitch chat: {1}", ModuleType.Name, message); 
        SendMethod.Invoke(null, new object[] {message});
    }
    
    /// <summary>
    /// Send a message to the Twitch chat
    /// </summary>
    /// <param name="message">Base string</param>
    /// <param name="args">Format arguments</param>
    protected void SendTwitchMessageFormat(string message, params object[] args) {
        SendTwitchMessage(String.Format(message, args));
    }
    #endregion
}
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(KMGameInfo))]
public abstract partial class KtaneModule : MonoBehaviour
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

    private static Dictionary<Type, int> LoggingIDs = new Dictionary<Type, int>();

    private const BindingFlags MainFlags = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// Called on the 0th frame
    /// </summary>
    protected virtual void Awake()
    {
        ModuleType = GetType();
        if (!DefaultCoroutineQueue.ContainsKey(ModuleType))
            DefaultCoroutineQueue.Add(
                ModuleType,
                new Dictionary<string, CoroutineQueue>()
                {
                    {DefaultID, new CoroutineQueue()}
                });
        if (!LoggingIDs.ContainsKey(ModuleType)) LoggingIDs.Add(ModuleType, 0);
        ModuleID = ++LoggingIDs[ModuleType];
    }

    /// <summary>
    /// Called on the 1st frame
    /// </summary>
    protected virtual void Start()
    {
        IsTestHarness = Application.isEditor;
        TwitchID = -1;
        TwitchGameType = ReflectionHelper.FindType("TwitchGame", "TwitchPlaysAssembly");

        GetComponent<KMGameInfo>().OnStateChange += state =>
        {
            foreach (var queueDict in DefaultCoroutineQueue)
            {
                LoggingIDs.Clear();
                foreach (var queue in queueDict.Value) queue.Value.Reset();
            }

            DefaultCoroutineQueue.Clear();
        };

        BombInfo = GetComponent<KMBombInfo>();
        BombModule = GetComponent<KMBombModule>();
        NeedyModule = GetComponent<KMNeedyModule>();
        BossModule = GetComponent("KMBossModule");
        if (BossModule != null)
            IgnoredMethod = BossModule
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
            var MissionType = MissionObject.GetType();
            MissionName = (string) MissionType.GetField("DisplayNameTerm", MainFlags)
                .GetValue(MissionObject);
            MissionID = (string) MissionType.GetProperty("ID", MainFlags).GetValue(MissionObject, null);

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

    /// <summary>
    /// Called on every frame
    /// </summary>
    protected virtual void Update()
    {
        if (BombInfo != null)
        {
            var solveds = BombInfo.GetSolvedModuleNames();
            var solvables = GetUnsolvedModuleNames();
            bool complete = IgnoredModules == null ? solvables.Count<=1 : solvables.All(IgnoredModules.Contains);
            if (solveds.Count > OldSolveds.Count)
            {
                var TempSolveds = solveds.ToList();
                foreach (string module in OldSolveds) TempSolveds.Remove(module);
                OldSolveds = solveds;
                foreach (string module in TempSolveds)
                {
                    if (WatchSolves && (IgnoredModules == null || !IgnoredModules.Contains(module))) OnNewStage(module, complete);
                }
            }

        }
        
        if (TwitchID < 1) TwitchID = GetTwitchID();
    }

    #endregion

    #region ModuleInfomation

    /// <summary>
    /// The KMBombInfo object of a module (if present)
    /// </summary>
    protected KMBombInfo BombInfo { get; private set; }

    /// <summary>
    /// The KMBombModule object of the module (if present)
    /// </summary>
    protected KMBombModule BombModule { get; private set; }
    
    /// <summary>
    /// The KMNeedyModule object of the module (if present)
    /// </summary>
    protected KMNeedyModule NeedyModule { get; private set; }

    /// <summary>
    /// Logging ID of the module
    /// </summary>
    protected int ModuleID { get; private set; }

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
    protected bool IsTestHarness { get; private set; }

    #endregion

    #region Missioninformation

    /// <summary>
    /// The internal Mission object of the current mission
    /// </summary>
    protected object MissionObject { get; private set; }

    /// <summary>
    /// The name of the current mission
    /// </summary>
    protected string MissionName { get; private set; }
    
    /// <summary>
    /// The ID of the current mission
    /// </summary>
    protected string MissionID { get; private set; }

    /// <summary>
    /// Gets the names of the unsolved modules
    /// </summary>
    /// <returns>The list of unsolved module names</returns>
    /// <exception cref="ModuleException">There is no KMBombInfo component attached</exception>
    protected List<string> GetUnsolvedModuleNames()
    {
        if(BombInfo==null) throw new ModuleException("There is no KMBombInfo component attached!");
        var AllModules = BombInfo.GetSolvableModuleNames();
        foreach (string module in BombInfo.GetSolvedModuleNames()) AllModules.Remove(module);
        return AllModules;
    }
    
    /// <summary>
    /// Gets the IDs of the unsolved modules
    /// </summary>
    /// <returns>The list of unsolved module IDs</returns>
    /// <exception cref="ModuleException">There is no KMBombInfo component attached</exception>
    protected List<string> GetUnsolvedModuleIDs()
    {
        if(BombInfo==null) throw new ModuleException("There is no KMBombInfo component attached!");
        var AllModules = BombInfo.GetSolvableModuleIDs();
        foreach (string module in BombInfo.GetSolvedModuleIDs()) AllModules.Remove(module);
        return AllModules;
    }

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
    protected Action<string, bool> OnNewStage = (ModuleName, Ready) => { };

    /// <summary>
    /// If false, the OnNewStage delegate won't be triggered
    /// </summary>
    protected bool WatchSolves = true;

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
    protected string[] GetIgnoredModules(KMBombModule Module, string[] @default = null)
    {
        return GetIgnoredModules(Module.ModuleDisplayName, @default);
    }
    #endregion
    
    #region CoroutineQueue
    private static Dictionary<Type, Dictionary<string, CoroutineQueue>> DefaultCoroutineQueue = new Dictionary<Type, Dictionary<string, CoroutineQueue>>();

    /// <summary>
    /// The ID of the default coroutine queue
    /// </summary>
    protected const string DefaultID = "default";
    
    /// <summary>
    /// Add coroutines to the queue
    /// </summary>
    /// <param name="id">The ID of the queue</param>
    /// <param name="SplitYields">If true, the next part of the coroutine will be added to the end of the queue when the one before ran</param>
    /// <param name="routines">Array of coroutines to enqueue</param>
    /// <exception cref="ArgumentException">The specified ID is either null or contains only whitespaces</exception>
    protected void QueueRoutines(string id, bool SplitYields, params IEnumerator[] routines)
    {
        if (String.IsNullOrEmpty(id.Trim())) throw new ArgumentException("Please specify a valid queue id!");
        if (!DefaultCoroutineQueue[ModuleType].ContainsKey(id)) DefaultCoroutineQueue[ModuleType].Add(id, new CoroutineQueue());
        DefaultCoroutineQueue[ModuleType][id].QueueRoutines(this, SplitYields, routines);
    }

    /// <summary>
    /// Add coroutines to the default queue
    /// </summary>
    /// <param name="routines">Array of coroutines to enqueue</param>
    protected void QueueRoutines(params IEnumerator[] routines)
    {
        QueueRoutines(DefaultID, false, routines);
    }

    /// <summary>
    /// Add coroutines to the default queue
    /// </summary>
    /// <param name="SplitYields">If true, the next part of the coroutine will be added to the end of the queue when the one before ran</param>
    /// <param name="routines">Array of coroutines to enqueue</param>
    protected void QueueRoutines(bool SplitYields, params IEnumerator[] routines)
    {
        QueueRoutines(DefaultID, SplitYields, routines);
    }

    /// <summary>
    /// Add coroutines to the queue
    /// </summary>
    /// <param name="id">The ID of the queue</param>
    /// <param name="routines">Array of coroutines to enqueue</param>
    /// <exception cref="ArgumentException">The specified ID is either null or contains only whitespaces</exception>
    protected void QueueRoutines(string id, params IEnumerator[] routines)
    {
        QueueRoutines(id, false, routines);
    }
    #endregion
    
    #region TwitchPlays
    private MethodInfo SendMethod = null;
    private Type TwitchGameType = null;
    
    private int GetTwitchID()
    {
        if (!IsTestHarness)
        {
            if (TwitchGameType != null)
            {
                object TwitchGame = FindObjectOfType(TwitchGameType);
                if (TwitchGame != null)
                {
                    IEnumerable TPModules = (IEnumerable)TwitchGameType.GetField("Modules", MainFlags).GetValue(TwitchGame);
                    foreach (object Module in TPModules)
                    {
                        Type ModuleType = Module.GetType();
                        var Behaviour =
                            (MonoBehaviour) (ModuleType.GetField("BombComponent", MainFlags).GetValue(Module));
                        if (Behaviour.GetComponent<KtaneModule>() == this)
                        {
                            return int.Parse((string) ModuleType.GetProperty("Code", MainFlags)
                                .GetValue(Module, null));
                        }
                    }
                }
            }
        }
        else
        {
            foreach (var component in GetComponentsInChildren<Component>(true))
            {
                Type cType = component.GetType();
                if (cType.ToString() == "TwitchPlaysID")
                {
                    return (int) cType.GetField("ModuleID", MainFlags).GetValue(component);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// ID of the module on TwitchPlays (-1 if TwitchPlays is not active)
    /// </summary>
    protected int TwitchID { get; private set; }
    
    /// <summary>
    /// Send a message to the Twitch chat
    /// </summary>
    /// <param name="message">The message to send</param>
    protected void SendTwitchMessage(string message)
    {
        if (!TwitchPlaysActive || SendMethod == null) return;
        Debug.LogFormat("[{0}] Sending message to Twitch chat: {1}", ModuleType.Name, message); 
        SendMethod.Invoke(null, new object[] {message});
    }
    
    /// <summary>
    /// Send a message to the Twitch chat
    /// </summary>
    /// <param name="message">Base message</param>
    /// <param name="args">Format arguments</param>
    protected void SendTwitchMessageFormat(string message, params object[] args) {
        SendTwitchMessage(String.Format(message, args));
    }
    #endregion
}
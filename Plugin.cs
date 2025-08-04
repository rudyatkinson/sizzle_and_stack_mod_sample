using System;
using System.Linq;
using ArvisGames.Lifescopes;
using ArvisGames.Scripts.Game.Account.Service;
using ArvisGames.Scripts.Game.Customer.Message;
using ArvisGames.Scripts.Game.Customer.Model;
using ArvisGames.Scripts.Game.Day.Service;
using ArvisGames.Scripts.Global.Database.Service;
using ArvisGames.Scripts.Global.GameConfig.Model;
using ArvisGames.Scripts.Global.MessagePipe.Extension;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace sizzleandstacksupercozy;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("SizzleAndStack.exe")]
public class Plugin : BaseUnityPlugin
{
    private const float ModifiedDailyReputation = 2f;
    private const float ModifiedCustomerReputation = 0.04f;
    private const float ModifiedReviewerReputation = 1f;
    private const float ModifiedGourmetReputation = 1f;
    
    internal static new ManualLogSource Logger;

    private IObjectResolver _container;
    private AccountService _accountService;
    
    private IDisposable _disposable;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        
        Invoke(nameof(Invoke), 1);
    }

    private void OnDestroy()
    {
        _disposable?.Dispose();
    }

    private void Invoke()
    {
        _container = FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None).ToList().Find(scope => scope.IsRoot).Container;
        _accountService = _container.Resolve<AccountService>();

        _container.Resolve<GameConfigModelSO>().MaxReputationEachDay = ModifiedDailyReputation;
        
        ModifyCustomers();
        ModifyItems();

        SubscribeMessages();
        
        Logger.LogInfo($"[SuperCozy] GetDependencies successfully initialized!");
    }

    private void SubscribeMessages()
    {
        var disposableBag = DisposableBag.CreateBuilder();

        _container
            .Resolve<ISubscriber<HappyCustomerMessage>>()
            .SafeSubscribe(OnCustomerLeaveHappy)
            .AddTo(disposableBag);
        
        _disposable = disposableBag.Build();
    }

    private void ModifyItems()
    {
        var databaseService = _container.Resolve<DatabaseService>();
        
        foreach (var item in databaseService.GetItems())
        {
            item.CurrencyReward *= 2;
        }
    }

    private void ModifyCustomers()
    {
        var databaseService = _container.Resolve<DatabaseService>();
        
        foreach (var customer in databaseService.GetCustomers())
        {
            switch (customer.Difficulty)
            {
                case CustomerDifficulty.Easy:
                case CustomerDifficulty.Medium:
                case CustomerDifficulty.Hard:
                    customer.ReputationReward = ModifiedCustomerReputation;
                    break;
                case CustomerDifficulty.Reviewer:
                    customer.ReputationReward = ModifiedReviewerReputation;
                    break;
                case CustomerDifficulty.Gourmet:
                    customer.ReputationReward = ModifiedGourmetReputation;
                    break;
            }
        }
    }

    private void OnCustomerLeaveHappy(HappyCustomerMessage obj)
    {
        if (!obj.IsHappy) return;
        
        _accountService.ChangeSatisfaction(1);
        FindObjectsByType<GameScope>(FindObjectsSortMode.None)[0].Container.Resolve<DayService>().ChangeDailySatisfaction(1);
    }
}

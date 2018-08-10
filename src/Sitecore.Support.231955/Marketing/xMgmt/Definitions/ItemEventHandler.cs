using Microsoft.Extensions.DependencyInjection;
using Sitecore.Abstractions;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.DependencyInjection;
using Sitecore.Events;
using Sitecore.Framework.Conditions;
using Sitecore.Marketing.Core.Extensions;
using Sitecore.Marketing.Definitions;
using Sitecore.Marketing.Definitions.AutomationPlans.Model;
using Sitecore.Marketing.Definitions.Campaigns;
using Sitecore.Marketing.Definitions.ContactLists;
using Sitecore.Marketing.Definitions.Events;
using Sitecore.Marketing.Definitions.Funnels;
using Sitecore.Marketing.Definitions.Goals;
using Sitecore.Marketing.Definitions.MarketingAssets;
using Sitecore.Marketing.Definitions.Outcomes.Model;
using Sitecore.Marketing.Definitions.PageEvents;
using Sitecore.Marketing.Definitions.Profiles;
using Sitecore.Marketing.Definitions.Segments;
using Sitecore.Marketing.xMgmt.Extensions;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using WellKnownIdentifiers = Sitecore.Marketing.Definitions.AutomationPlans.WellKnownIdentifiers;

namespace Sitecore.Support.Marketing.xMgmt.Definitions
{
  public class ItemEventHandler
  {
    internal static readonly IEnumerable<ID> MarketingContainers = new[]
    {
            WellKnownIdentifiers.MarketingCenterAutomationPlanContainerId.ToID(),
            Sitecore.Marketing.Definitions.Campaigns.WellKnownIdentifiers.MarketingCenterCampaignsContainerId.ToID(),
            Sitecore.Marketing.Definitions.Events.WellKnownIdentifiers.EventsContainerId.ToID(),
            Sitecore.Marketing.Definitions.Funnels.WellKnownIdentifiers.MarketingCenterFunnelsContainerId.ToID(),
            Sitecore.Marketing.Definitions.Goals.WellKnownIdentifiers.MarketingCenterGoalsContainerId.ToID(),
            ItemIDs.MediaLibraryRoot,
            Sitecore.Marketing.Definitions.Outcomes.WellKnownIdentifiers.MarketingCenterOutcomeContainerId.ToID(),
            Sitecore.Marketing.Definitions.PageEvents.WellKnownIdentifiers.PageEventsContainerId.ToID(),
            Sitecore.Marketing.Definitions.Profiles.WellKnownIdentifiers.ProfilesContainerId.ToID(),
            Sitecore.Marketing.Definitions.ContactLists.WellKnownIdentifiers.ContactListsContainerId.ToID(),
            Sitecore.Marketing.Definitions.Segments.WellKnownIdentifiers.MarketingCenterSegmentsContainerId.ToID()
        };

    private static int _disabled;

    private readonly BaseTemplateManager _templateManager;
    private readonly DefinitionManagerFactory _definitionManagerFactory;
    private readonly BaseStandardValuesManager _baseStandardValuesManager;
    private readonly BaseLog _log;

    public ItemEventHandler() : this(ServiceLocator.ServiceProvider.GetRequiredService<BaseTemplateManager>(),
        ServiceLocator.ServiceProvider.GetRequiredService<DefinitionManagerFactory>(),
        ServiceLocator.ServiceProvider.GetRequiredService<BaseStandardValuesManager>(),
        ServiceLocator.ServiceProvider.GetRequiredService<BaseLog>())
    {
    }

    internal ItemEventHandler([NotNull] BaseTemplateManager templateManager,
        [NotNull] DefinitionManagerFactory definitionManagerFactory,
        [NotNull] BaseStandardValuesManager baseStandardValuesManager,
        [NotNull] BaseLog log)
    {
      Condition.Requires(templateManager, nameof(templateManager)).IsNotNull();
      Condition.Requires(definitionManagerFactory, nameof(definitionManagerFactory)).IsNotNull();
      Condition.Requires(baseStandardValuesManager, nameof(baseStandardValuesManager)).IsNotNull();
      Condition.Requires(log, nameof(log)).IsNotNull();

      _templateManager = templateManager;
      _definitionManagerFactory = definitionManagerFactory;
      _baseStandardValuesManager = baseStandardValuesManager;
      _log = log;
    }

    public static void Disable()
    {
      Interlocked.Increment(ref _disabled);
    }

    public static void Enable()
    {
      Interlocked.Decrement(ref _disabled);
    }

    internal static bool ShouldSkipEventProcessing(EventArgs args)
    {
      return args == null || _disabled > 0 || Context.Site == null || Context.Site.Name == "publisher";
    }

    private void ProcessValidationException(Exception ex, string name, string parentPath)
    {
      if (AjaxScriptManager.Current != null || (Sitecore.Context.ClientPage != null && Sitecore.Context.ClientPage.ClientResponse != null))
      {
        SheerResponse.ShowError(ex);
      }
      _log.Error($"{name} is not a valid item name under {parentPath}. {ex.Message}", this);
    }

    protected void ValidateItemName(EventArgs args)
    {
      if (ShouldSkipEventProcessing(args))
      {
        return;
      }

      var parameters = Event.ExtractParameters(args);
      var item = parameters[0] as Item;
      var parent = item.Parent;
      if (parameters.Length >= 3 && (parameters[2] is ID))
      {
        var parentID = parameters[2] as ID;
        parent = item.Database.GetItem(parentID);
      }
      var itemID = item.ID;
      if (parameters.Length >= 4 && (parameters[3] is ID))
      {
        itemID = parameters[3] as ID;
      }

      if (item != null)
      {
        try
        {
          var itemData = new ItemData
          {
            Name = item.Name,
            Id = itemID,
            TemplateId = item.TemplateID,
            Parent = parent,
            ItemDb = item.Database
          };

          ValidateItemName(itemData);
        }
        catch (Exception ex)
        {
          var sArgs = args as Sitecore.Events.SitecoreEventArgs;
          if (sArgs != null)
          {
            sArgs.Result.Cancel = true;
          }
          ProcessValidationException(ex, item.Name, item.Parent.Paths.FullPath);
        }
      }
    }

    [UsedImplicitly]
    protected internal void OnItemMoving([NotNull] object sender, [CanBeNull] EventArgs args)
    {
      Condition.Requires(sender, nameof(sender)).IsNotNull();
      ValidateItemName(args);
    }

    [UsedImplicitly]
    protected internal void OnItemCopying([NotNull] object sender, [CanBeNull] EventArgs args)
    {
      Condition.Requires(sender, nameof(sender)).IsNotNull();
      ValidateItemName(args);
    }

    [UsedImplicitly]
    protected internal void OnItemRenaming([NotNull] object sender, [CanBeNull] EventArgs args)
    {
      Condition.Requires(sender, nameof(sender)).IsNotNull();
      ValidateItemName(args);
    }

    [UsedImplicitly]
    protected internal void OnItemSaving([NotNull] object sender, [CanBeNull] EventArgs args)
    {
      Condition.Requires(sender, nameof(sender)).IsNotNull();
      ValidateItemName(args);
    }

    [UsedImplicitly]
    protected internal void OnItemCreating([NotNull] object sender, [CanBeNull] EventArgs args)
    {
      Condition.Requires(sender, nameof(sender)).IsNotNull();

      if (ShouldSkipEventProcessing(args))
      {
        return;
      }

      ItemCreatingEventArgs creatingArgs = Event.ExtractParameter(args, 0) as ItemCreatingEventArgs;
      if (creatingArgs != null)
      {
        try
        {
          var itemData = new ItemData
          {
            Name = creatingArgs.ItemName,
            Id = creatingArgs.ItemId,
            TemplateId = creatingArgs.TemplateId,
            Parent = creatingArgs.Parent,
            ItemDb = creatingArgs.Parent.Database
          };

          ValidateItemName(itemData);
        }
        catch (Exception ex)
        {
          creatingArgs.Cancel = true;
          ProcessValidationException(ex, creatingArgs.ItemName, creatingArgs.Parent.Paths.FullPath);
        }
      }
    }

    private static bool IsUnderMarketingContainer(Item parent)
    {
      return parent != null && (MarketingContainers.Contains(parent.ID) || IsUnderMarketingContainer(parent.Parent));
    }

    private void ValidateItemName(ItemData itemData)
    {
      if (!IsUnderMarketingContainer(itemData.Parent))
      {
        return;
      }

      Template itemTemplate = _templateManager.GetTemplate(itemData.TemplateId, itemData.ItemDb);
      if (itemTemplate == null)
      {
        _log.SingleWarn(FormattableString.Invariant($"Template with ID {itemData.TemplateId} not found"), this);
        return;
      }

      if (IsMarketingDefinition(_templateManager, itemTemplate))
      {
        Condition.Ensures(itemData.Name, nameof(itemData.Name)).IsValidAlias();

        Dictionary<Guid, HashSet<Guid>> templatesInheritanceDictionary = GetTemplatesInheritanceDictionary(_templateManager, itemData.ItemDb, Sitecore.Marketing.Definitions.WellKnownIdentifiers.MarketingDefinition.DefinitionTemplateIds);

        ValidateAlias<IAutomationPlanDefinition>(itemData, itemTemplate, WellKnownIdentifiers.PlanDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<ICampaignActivityDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Campaigns.WellKnownIdentifiers.CampaignActivityDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IEventDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Events.WellKnownIdentifiers.EventDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IFunnelDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Funnels.WellKnownIdentifiers.FunnelDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IGoalDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Goals.WellKnownIdentifiers.GoalDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IOutcomeDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Outcomes.WellKnownIdentifiers.OutcomeDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IPageEventDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.PageEvents.WellKnownIdentifiers.PageEventDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IProfileDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Profiles.WellKnownIdentifiers.ProfileDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IContactListDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.ContactLists.WellKnownIdentifiers.ContactListDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<ISegmentDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.Segments.WellKnownIdentifiers.SegmentDefinitionTemplateId, templatesInheritanceDictionary);
        ValidateAlias<IMarketingAssetDefinition>(itemData, itemTemplate, Sitecore.Marketing.Definitions.MarketingAssets.WellKnownIdentifiers.TemplateIDs.MediaClassificationTemplateId, templatesInheritanceDictionary);

        return;
      }

      if (IsNestedMarketingDefinition(_templateManager, itemTemplate))
      {
        Condition.Ensures(itemData.Name, nameof(itemData.Name)).IsValidAlias();

        IProfileDefinition profile = LoadProfile(itemData);

        if (profile == null)
        {
          return;
        }

        if (itemTemplate.InheritsFrom(Sitecore.Marketing.Definitions.Profiles.WellKnownIdentifiers.ProfileKeyTemplateId.ToID()))
        {
          if (profile.Keys.Any(k => k.Alias.Equals(itemData.Name, StringComparison.InvariantCultureIgnoreCase) && k.Id.ToID() != itemData.Id))
          {
            throw new InvalidOperationException("Profile key item name is not unique within the profile definition. Please choose another item name.");
          }

          return;
        }

        if (itemTemplate.InheritsFrom(Sitecore.Marketing.Definitions.Profiles.WellKnownIdentifiers.ProfileCardTemplateId.ToID()))
        {
          if (profile.ProfileCards.Any(k => k.Alias.Equals(itemData.Name, StringComparison.InvariantCultureIgnoreCase) && k.Id.ToID() != itemData.Id))
          {
            throw new InvalidOperationException("Profile card item name is not unique within the profile definition. Please choose another item name.");
          }

          return;
        }

        if (itemTemplate.InheritsFrom(Sitecore.Marketing.Definitions.Profiles.WellKnownIdentifiers.PatternCardTemplateId.ToID()))
        {
          if (profile.Patterns.Any(k => k.Alias.Equals(itemData.Name, StringComparison.InvariantCultureIgnoreCase) && k.Id.ToID() != itemData.Id))
          {
            throw new InvalidOperationException("Pattern card item name is not unique within the profile definition. Please choose another item name.");
          }
        }
      }
    }

    [CanBeNull]
    private IProfileDefinition LoadProfile(ItemData itemData)
    {
      Guid? id = GetDefinitionParentId(itemData.ItemDb, itemData.Parent.ID);

      if (id == null)
      {
        return null;
      }

      IDefinitionManager<IProfileDefinition> manager = _definitionManagerFactory.GetDefinitionManager<IProfileDefinition>();
      IProfileDefinition profile = manager.Get(id.Value, CultureInfo.InvariantCulture, true);
      return profile;
    }

    private void ValidateAlias<TDefinitionInterface>(ItemData itemData, Template itemTemplate, Guid expectedTemplateId, Dictionary<Guid, HashSet<Guid>> templateIdsInheritanceDictionary)
        where TDefinitionInterface : IDefinition
    {
      if (itemTemplate.InheritsFrom(expectedTemplateId.ToID()))
      {
        HashSet<Guid> childIds;
        if (!templateIdsInheritanceDictionary.TryGetValue(expectedTemplateId, out childIds))
        {
          throw new InvalidOperationException(FormattableString.Invariant($"Unknown definition template id '{expectedTemplateId}'"));
        }

        if (!childIds.Any(c => itemTemplate.InheritsFrom(c.ToID())))
        {
          IDefinitionManager<TDefinitionInterface> sourceManager = _definitionManagerFactory.GetDefinitionManager<TDefinitionInterface>();
          TDefinitionInterface currentDefinition = sourceManager.GetByAlias(itemData.Name, CultureInfo.InvariantCulture, true);

          if (currentDefinition != null && currentDefinition.Id != itemData.Id.Guid)
          {
            throw new InvalidOperationException("Item name is not unique within the definition. Please choose another item name.");
          }
        }
      }
    }

    private Guid? GetDefinitionParentId(Database itemDb, ID itemParentId)
    {
      while (!ID.IsNullOrEmpty(itemParentId))
      {
        Item parent = itemDb.GetItem(itemParentId);

        if (parent == null)
        {
          break;
        }

        if (IsMarketingDefinition(_templateManager, parent))
        {
          return parent.ID.Guid;
        }

        itemParentId = parent.ParentID;
      }

      return null;
    }

    private Item FindTopParentDeployableDefinition(Item item)
    {
      if (item?.Parent == null || !IsDeployableDefinition(item.Parent))
      {
        return item;
      }

      return FindTopParentDeployableDefinition(item.Parent);
    }

    private bool IsDeployableDefinition(Item item)
    {
      Template template = _templateManager.GetTemplate(item);

      return template != null &&
          Sitecore.Marketing.Definitions.WellKnownIdentifiers.MarketingDefinition.DefinitionTemplateIds
              .Any(templateId => template.InheritsFrom(templateId.ToID()));
    }

    private class ItemData
    {
      public string Name { get; set; }

      public ID Id { get; set; }

      public ID TemplateId { get; set; }

      public Item Parent { get; set; }

      public Database ItemDb { get; set; }
    }

    private bool IsMarketingDefinition(BaseTemplateManager templateManager, Item item)
    {
      Condition.Requires(templateManager, nameof(templateManager)).IsNotNull();
      Condition.Requires(item, nameof(item)).IsNotNull();

      Template template = templateManager.GetTemplate(item);

      return IsMarketingDefinition(templateManager, template);
    }

    private bool IsMarketingDefinition(
        [NotNull] BaseTemplateManager templateManager,
        [NotNull] Template itemTemplate)
    {
      Condition.Requires(templateManager, nameof(templateManager)).IsNotNull();
      Condition.Requires(itemTemplate, nameof(itemTemplate)).IsNotNull();

      return Sitecore.Marketing.Definitions.WellKnownIdentifiers.MarketingDefinition.DefinitionTemplateIds
              .Any(d => itemTemplate.InheritsFrom(d.ToID()));
    }

    private Dictionary<Guid, HashSet<Guid>> GetTemplatesInheritanceDictionary(BaseTemplateManager templateManager, Database database, IEnumerable<Guid> definitionTemplateIds)
    {
      var result = new Dictionary<Guid, HashSet<Guid>>();

      foreach (Guid definitionTemplateId in definitionTemplateIds)
      {
        if (!result.ContainsKey(definitionTemplateId))
        {
          result.Add(definitionTemplateId, new HashSet<Guid>());
        }

        TemplateList baseTemplates = templateManager.GetTemplate(definitionTemplateId.ToID(), database)?.GetBaseTemplates();

        if (baseTemplates == null)
        {
          continue;
        }

        foreach (Template baseTemplate in baseTemplates)
        {
          Guid templateId = baseTemplate.ID.Guid;

          if (!result.ContainsKey(templateId))
          {
            result.Add(templateId, new HashSet<Guid>());
          }

          result[templateId].Add(definitionTemplateId);
        }
      }

      return result;
    }

    private bool IsNestedMarketingDefinition(BaseTemplateManager templateManager, Template itemTemplate)
    {
      return Sitecore.Marketing.Definitions.Profiles.WellKnownIdentifiers.ProfileDefinitionNestedItemsTemplateIds.Any(d => itemTemplate.InheritsFrom(d.ToID()));
    }
  }
}

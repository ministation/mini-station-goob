using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Botany.PlantAnalyzer;
using Content.Shared.IdentityManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Botany.PlantAnalyzer;

public sealed partial class PlantAnalyzerWindow : FancyWindow
{
    private readonly IEntityManager _entityManager;
    private readonly IPrototypeManager _prototypeManager;
    private readonly IGameTiming _gameTiming;
    private readonly SpriteView _spriteView;
    private readonly TextureRect _noDataIcon;
    private readonly Label _scanModeLabel;
    private readonly RichTextLabel _seedLabel;
    private readonly Label _containerLabel;
    private readonly Label _health;
    private readonly Label _endurance;
    private readonly Label _age;
    private readonly Label _lifespan;
    private readonly RichTextLabel _alive;
    private readonly RichTextLabel _dead;
    private readonly RichTextLabel _unviable;
    private readonly RichTextLabel _kudzu;
    private readonly RichTextLabel _mutating;
    private readonly GridContainer _plantDataGrid;
    private readonly BoxContainer _plantDataTags;
    private readonly PanelContainer _plantDataDivider;
    private readonly Label _waterLevelLabel;
    private readonly Label _nutritionLevelLabel;
    private readonly Label _toxinsLabel;
    private readonly Label _pestLevelLabel;
    private readonly Label _weedLevelLabel;
    private readonly Label _gtFieldIfTolerances1;
    private readonly Label _gtFieldIfTolerances2;
    private readonly Label _ltFieldIfTolerances1;
    private readonly Label _ltFieldIfTolerances2;
    private readonly Label _ltFieldIfTolerances3;
    private readonly Label _waterConsumptionLabel;
    private readonly Label _nutritionConsumptionLabel;
    private readonly Label _toxinsResistanceLabel;
    private readonly Label _pestResistanceLabel;
    private readonly Label _weedResistanceLabel;
    private readonly GridContainer _containerGrid;
    private readonly PanelContainer _containerDivider;
    private readonly RichTextLabel _chemicalsInWaterLabel;
    private readonly BoxContainer _chemicalsInWaterBox;
    private readonly PanelContainer _chemicalsInWaterDivider;
    private readonly RichTextLabel _environmentLabel;
    private readonly BoxContainer _environmentBox;
    private readonly PanelContainer _environmentDivider;
    private readonly RichTextLabel _produceLabel;
    private readonly BoxContainer _produceBox;
    private readonly PanelContainer _produceDivider;

    public Button Print { get; }

    public PlantAnalyzerWindow()
    {
        RobustXamlLoader.Load(this);

        _spriteView = FindControl<SpriteView>("SpriteView");
        _noDataIcon = FindControl<TextureRect>("NoDataIcon");
        _scanModeLabel = FindControl<Label>("ScanModeLabel");
        _seedLabel = FindControl<RichTextLabel>("SeedLabel");
        _containerLabel = FindControl<Label>("ContainerLabel");
        _health = FindControl<Label>("Health");
        _endurance = FindControl<Label>("Endurance");
        _age = FindControl<Label>("Age");
        _lifespan = FindControl<Label>("Lifespan");
        _alive = FindControl<RichTextLabel>("Alive");
        _dead = FindControl<RichTextLabel>("Dead");
        _unviable = FindControl<RichTextLabel>("Unviable");
        _kudzu = FindControl<RichTextLabel>("Kudzu");
        _mutating = FindControl<RichTextLabel>("Mutating");
        _plantDataGrid = FindControl<GridContainer>("PlantDataGrid");
        _plantDataTags = FindControl<BoxContainer>("PlantDataTags");
        _plantDataDivider = FindControl<PanelContainer>("PlantDataDivider");
        _waterLevelLabel = FindControl<Label>("WaterLevelLabel");
        _nutritionLevelLabel = FindControl<Label>("NutritionLevelLabel");
        _toxinsLabel = FindControl<Label>("ToxinsLabel");
        _pestLevelLabel = FindControl<Label>("PestLevelLabel");
        _weedLevelLabel = FindControl<Label>("WeedLevelLabel");
        _gtFieldIfTolerances1 = FindControl<Label>("GtFieldIfTolerances1");
        _gtFieldIfTolerances2 = FindControl<Label>("GtFieldIfTolerances2");
        _ltFieldIfTolerances1 = FindControl<Label>("LtFieldIfTolerances1");
        _ltFieldIfTolerances2 = FindControl<Label>("LtFieldIfTolerances2");
        _ltFieldIfTolerances3 = FindControl<Label>("LtFieldIfTolerances3");
        _waterConsumptionLabel = FindControl<Label>("WaterConsumptionLabel");
        _nutritionConsumptionLabel = FindControl<Label>("NutritionConsumptionLabel");
        _toxinsResistanceLabel = FindControl<Label>("ToxinsResistanceLabel");
        _pestResistanceLabel = FindControl<Label>("PestResistanceLabel");
        _weedResistanceLabel = FindControl<Label>("WeedResistanceLabel");
        _containerGrid = FindControl<GridContainer>("ContainerGrid");
        _containerDivider = FindControl<PanelContainer>("ContainerDivider");
        _chemicalsInWaterLabel = FindControl<RichTextLabel>("ChemicalsInWaterLabel");
        _chemicalsInWaterBox = FindControl<BoxContainer>("ChemicalsInWaterBox");
        _chemicalsInWaterDivider = FindControl<PanelContainer>("ChemicalsInWaterDivider");
        _environmentLabel = FindControl<RichTextLabel>("EnvironmentLabel");
        _environmentBox = FindControl<BoxContainer>("EnvironmentBox");
        _environmentDivider = FindControl<PanelContainer>("EnvironmentDivider");
        _produceLabel = FindControl<RichTextLabel>("ProduceLabel");
        _produceBox = FindControl<BoxContainer>("ProduceBox");
        _produceDivider = FindControl<PanelContainer>("ProduceDivider");
        Print = FindControl<Button>("Print");

        var dependencies = IoCManager.Instance!;
        _entityManager = dependencies.Resolve<IEntityManager>();
        _prototypeManager = dependencies.Resolve<IPrototypeManager>();
        _gameTiming = dependencies.Resolve<IGameTiming>();
    }

    public void Populate(PlantAnalyzerScannedUserMessage msg)
    {
        var SpriteView = this.FindControl<SpriteView>("SpriteView");
        var NoDataIcon = this.FindControl<TextureRect>("NoDataIcon");
        var ScanModeLabel = this.FindControl<Label>("ScanModeLabel");
        var SeedLabel = this.FindControl<RichTextLabel>("SeedLabel");
        var ContainerLabel = this.FindControl<Label>("ContainerLabel");
        var Health = this.FindControl<Label>("Health");
        var Endurance = this.FindControl<Label>("Endurance");
        var Age = this.FindControl<Label>("Age");
        var Lifespan = this.FindControl<Label>("Lifespan");
        var Dead = this.FindControl<RichTextLabel>("Dead");
        var Alive = this.FindControl<RichTextLabel>("Alive");
        var Unviable = this.FindControl<RichTextLabel>("Unviable");
        var Mutating = this.FindControl<RichTextLabel>("Mutating");
        var Kudzu = this.FindControl<RichTextLabel>("Kudzu");
        var PlantDataGrid = this.FindControl<GridContainer>("PlantDataGrid");
        var PlantDataTags = this.FindControl<BoxContainer>("PlantDataTags");
        var PlantDataDivider = this.FindControl<PanelContainer>("PlantDataDivider");
        var WaterLevelLabel = this.FindControl<Label>("WaterLevelLabel");
        var NutritionLevelLabel = this.FindControl<Label>("NutritionLevelLabel");
        var ToxinsLabel = this.FindControl<Label>("ToxinsLabel");
        var PestLevelLabel = this.FindControl<Label>("PestLevelLabel");
        var WeedLevelLabel = this.FindControl<Label>("WeedLevelLabel");
        var GtFieldIfTolerances1 = this.FindControl<Label>("GtFieldIfTolerances1");
        var LtFieldIfTolerances1 = this.FindControl<Label>("LtFieldIfTolerances1");
        var WaterConsumptionLabel = this.FindControl<Label>("WaterConsumptionLabel");
        var NutritionConsumptionLabel = this.FindControl<Label>("NutritionConsumptionLabel");
        var ToxinsResistanceLabel = this.FindControl<Label>("ToxinsResistanceLabel");
        var PestResistanceLabel = this.FindControl<Label>("PestResistanceLabel");
        var WeedResistanceLabel = this.FindControl<Label>("WeedResistanceLabel");
        var GtFieldIfTolerances2 = this.FindControl<Label>("GtFieldIfTolerances2");
        var LtFieldIfTolerances2 = this.FindControl<Label>("LtFieldIfTolerances2");
        var LtFieldIfTolerances3 = this.FindControl<Label>("LtFieldIfTolerances3");
        var ContainerGrid = this.FindControl<GridContainer>("ContainerGrid");
        var ContainerDivider = this.FindControl<PanelContainer>("ContainerDivider");
        var ChemicalsInWaterLabel = this.FindControl<RichTextLabel>("ChemicalsInWaterLabel");
        var ChemicalsInWaterBox = this.FindControl<BoxContainer>("ChemicalsInWaterBox");
        var ChemicalsInWaterDivider = this.FindControl<PanelContainer>("ChemicalsInWaterDivider");
        var EnvironmentLabel = this.FindControl<RichTextLabel>("EnvironmentLabel");
        var EnvironmentBox = this.FindControl<BoxContainer>("EnvironmentBox");
        var EnvironmentDivider = this.FindControl<PanelContainer>("EnvironmentDivider");
        var ProduceLabel = this.FindControl<RichTextLabel>("ProduceLabel");
        var ProduceBox = this.FindControl<BoxContainer>("ProduceBox");
        var ProduceDivider = this.FindControl<PanelContainer>("ProduceDivider");

        Print.Disabled = !msg.ScanMode.GetValueOrDefault(false)
            || msg.PrintReadyAt.GetValueOrDefault(TimeSpan.MaxValue) > _gameTiming.CurTime
            || msg.PlantData is null;

        var target = _entityManager.GetEntity(msg.TargetEntity);
        if (target is null)
        {
            return;
        }

        // Section 1: Icon and basic information.
        _spriteView.SetEntity(target.Value);
        _spriteView.Visible = msg.ScanMode.HasValue && msg.ScanMode.Value;
        _noDataIcon.Visible = !_spriteView.Visible;

        _scanModeLabel.Text = msg.ScanMode.HasValue
            ? msg.ScanMode.Value
                ? Loc.GetString("health-analyzer-window-scan-mode-active")
                : Loc.GetString("health-analyzer-window-scan-mode-inactive")
            : Loc.GetString("health-analyzer-window-entity-unknown-text");
        _scanModeLabel.FontColorOverride = msg.ScanMode.HasValue && msg.ScanMode.Value ? Color.Green : Color.Red;

        _seedLabel.Text = msg.PlantData == null
            ? Loc.GetString("plant-analyzer-component-no-seed")
            : Loc.GetString(msg.PlantData.SeedDisplayName);

        _containerLabel.Text = _entityManager.HasComponent<MetaDataComponent>(target.Value)
            ? Identity.Name(target.Value, _entityManager)
            : Loc.GetString("generic-unknown");

        // Section 2: Information regarding the plant.
        if (msg.PlantData is not null)
        {
            _health.Text = msg.PlantData.Health.ToString("0.00");
            _endurance.Text = msg.PlantData.Endurance.ToString("0.00");
            _age.Text = msg.PlantData.Age.ToString("0.00");
            _lifespan.Text = msg.PlantData.Lifespan.ToString("0.00");

            // These mostly exists to prevent shifting of the text.
            _dead.Visible = msg.PlantData.Dead;
            _alive.Visible = !_dead.Visible;

            _unviable.Visible = !msg.PlantData.Viable;
            _mutating.Visible = msg.PlantData.Mutating;
            _kudzu.Visible = msg.PlantData.Kudzu;

            _plantDataGrid.Visible = true;
        }
        else
        {
            _plantDataGrid.Visible = false;
        }
        _plantDataTags.Visible = _plantDataGrid.Visible;
        _plantDataDivider.Visible = _plantDataGrid.Visible;

        // Section 3: Input
        if (msg.TrayData is not null)
        {
            _waterLevelLabel.Text = msg.TrayData.WaterLevel.ToString("0.00");
            _nutritionLevelLabel.Text = msg.TrayData.NutritionLevel.ToString("0.00");
            _toxinsLabel.Text = msg.TrayData.Toxins.ToString("0.00");
            _pestLevelLabel.Text = msg.TrayData.PestLevel.ToString("0.00");
            _weedLevelLabel.Text = msg.TrayData.WeedLevel.ToString("0.00");

            // Section 3.1: Tolerances part 1.
            if (msg.TolerancesData is not null)
            {
                _gtFieldIfTolerances1.Text = ">";
                _ltFieldIfTolerances1.Text = "<";

                _waterConsumptionLabel.Text = msg.TolerancesData.WaterConsumption.ToString("0.00");
                _nutritionConsumptionLabel.Text = msg.TolerancesData.NutrientConsumption.ToString("0.00");
                // Technically would be "x + epsilon" for toxin and pest.
                // But it makes no difference here since I only display two digits.
                _toxinsResistanceLabel.Text = msg.TolerancesData.ToxinsTolerance.ToString("0.00");
                _pestResistanceLabel.Text = msg.TolerancesData.PestTolerance.ToString("0.00");
                _weedResistanceLabel.Text = msg.TolerancesData.WeedTolerance.ToString("0.00");
            }
            else
            {
                _gtFieldIfTolerances1.Text = "";
                _ltFieldIfTolerances1.Text = "";

                _waterConsumptionLabel.Text = "";
                _nutritionConsumptionLabel.Text = "";
                _toxinsResistanceLabel.Text = "";
                _pestResistanceLabel.Text = "";
                _weedResistanceLabel.Text = "";
            }
            _gtFieldIfTolerances2.Text = _gtFieldIfTolerances1.Text;
            _ltFieldIfTolerances2.Text = _ltFieldIfTolerances1.Text;
            _ltFieldIfTolerances3.Text = _ltFieldIfTolerances1.Text;

            _containerGrid.Visible = true;
        }
        else
        {
            _containerGrid.Visible = false;
        }
        _containerDivider.Visible = _containerGrid.Visible;


        // Section 3.5: They are putting chemicals in the water!
        if (msg.TrayData?.Chemicals != null)
        {
            var count = msg.TrayData.Chemicals.Count;
            var holder = _containerLabel.Text ?? string.Empty;
            var chemicals = PlantAnalyzerLocalizationHelper.ChemicalsToLocalizedStrings(msg.TrayData.Chemicals, _prototypeManager);
            if (count == 0)
                _chemicalsInWaterLabel.Text = Loc.GetString("plant-analyzer-soil-empty", ("holder", holder));
            else
                _chemicalsInWaterLabel.Text = Loc.GetString("plant-analyzer-soil", ("count", count), ("holder", holder), ("chemicals", chemicals));

            _chemicalsInWaterBox.Visible = true;
        }
        else
        {
            _chemicalsInWaterBox.Visible = false;
        }
        _chemicalsInWaterDivider.Visible = _chemicalsInWaterBox.Visible;

        // Section 4: Tolerances part 2.
        if (msg.TolerancesData is not null)
        {
            (string, string)[] parameters = [
                ("seedName", _seedLabel.Text),
                ("gases", PlantAnalyzerLocalizationHelper.GasesToLocalizedStrings(msg.TolerancesData.ConsumeGasses, _prototypeManager)),
                ("kpa", msg.TolerancesData.IdealPressure.ToString("0.00")),
                ("kpaTolerance", msg.TolerancesData.PressureTolerance.ToString("0.00")),
                ("temp", msg.TolerancesData.IdealHeat.ToString("0.00")),
                ("tempTolerance", msg.TolerancesData.HeatTolerance.ToString("0.00")),
                ("lightLevel", msg.TolerancesData.IdealLight.ToString("0.00")),
                ("lightTolerance", msg.TolerancesData.LightTolerance.ToString("0.00"))
            ];
            _environmentLabel.Text = msg.TolerancesData.ConsumeGasses.Count == 0
                ? msg.TolerancesData.IdealHeat - msg.TolerancesData.HeatTolerance <= 0f && msg.TolerancesData.IdealPressure - msg.TolerancesData.PressureTolerance <= 0f
                    ? Loc.GetString("plant-analyzer-component-environemt-void", [.. parameters])
                    : Loc.GetString("plant-analyzer-component-environemt", [.. parameters])
                : Loc.GetString("plant-analyzer-component-environemt-gas", [.. parameters]);

            _environmentBox.Visible = true;
        }
        else
        {
            _environmentBox.Visible = false;
        }
        _environmentDivider.Visible = _environmentBox.Visible;

        // Section 5: Output
        if (msg.ProduceData is not null)
        {
            var gases = PlantAnalyzerLocalizationHelper.GasesToLocalizedStrings(msg.ProduceData.ExudeGasses, _prototypeManager);
            var (produce, producePlural) = PlantAnalyzerLocalizationHelper.ProduceToLocalizedStrings(msg.ProduceData.Produce, _prototypeManager);
            var chemicals = PlantAnalyzerLocalizationHelper.ChemicalsToLocalizedStrings(msg.ProduceData.Chemicals, _prototypeManager);

            (string, object)[] parameters = [
                ("yield", msg.ProduceData.Yield),
                ("gasCount", msg.ProduceData.ExudeGasses.Count),
                ("gases", gases),
                ("potency", Loc.GetString(msg.ProduceData.Potency)),
                ("seedless", msg.ProduceData.Seedless),
                ("firstProduce", msg.ProduceData.Produce.FirstOrDefault() ?? ""),
                ("produce", produce),
                ("producePlural", producePlural),
                ("chemCount", msg.ProduceData.Chemicals.Count),
                ("chemicals", chemicals),
                ("nothing", "")
            ];

            _produceLabel.Text = Loc.GetString("plant-analyzer-output", [.. parameters]);
            _produceBox.Visible = true;
        }
        else
        {
            _produceBox.Visible = false;
        }
        _produceDivider.Visible = _produceBox.Visible;
    }
}

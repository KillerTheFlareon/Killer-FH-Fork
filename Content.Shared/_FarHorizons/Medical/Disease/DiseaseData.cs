using Content.Shared._FarHorizons.Medical.Disease.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Medical.Disease.Systems;

[Serializable, NetSerializable]
[DataRecord]
public partial record struct DiseaseData
{
    /// <summary>
    /// The prototype for this disease.
    /// </summary>
    [ViewVariables]
    public ProtoId<DiseasePrototype> Id;

    /// <summary>
    /// Displayed name of the disease.
    /// </summary>
    [DataField(required: true)]
    public string Name = default!;

    /// <summary>
    /// Displayed description of the disease.
    /// </summary>
    [DataField(required: true)]
    public string Description = default!;

    /// <summary>
    /// Randomized name for the strain of the disease.
    /// </summary>
    [DataField]
    public string StrainName = string.Empty;

    /// <summary>
    /// Spread vectors for this disease.
    /// </summary>
    [DataField]
    public DiseaseSpreadPath SpreadPath = DiseaseSpreadPath.NonContagious;

    /// <summary>
    /// Probability of progression through disease stages per tick.
    /// </summary>
    [DataField]
    public float StageProb = 0.02f;

    /// <summary>
    /// Default immunity strength granted after curing this disease (0-1).
    /// </summary>
    [DataField]
    public float PostCureImmunity = 0.7f;

    /// <summary>
    /// Optional incubation time in seconds before symptoms/spread begin after infection.
    /// </summary>
    [DataField]
    public float IncubationSeconds;

    /// <summary>
    /// Per-disease permeability multiplier (0-1) applied to PPE/internals effectiveness.
    /// Values > 1 reduce protection; values < 1 increase protection.
    /// </summary>
    [DataField]
    public float PermeabilityMod = 1.0f;

    /// <summary>
    /// Base per-contact infection probability for this disease (0-1). Used when two entities make contact.
    /// </summary>
    [DataField]
    public float ContactInfect = 0.025f;

    /// <summary>
    /// Amount of residue intensity deposited when a carrier with this disease contacts a surface.
    /// Expressed as (0-1) fraction added to per-disease residue intensity.
    /// </summary>
    [DataField]
    public float ContactDeposit = 0.2f;

    /// <summary>
    /// Base per-target airborne infection probability (0-1) before PPE adjustments.
    /// </summary>
    [DataField]
    public float AirborneInfect = 0.025f;

    /// <summary>
    /// Airborne infection radius in world units, used when <see cref="SpreadPath"/> contains Airborne.
    /// </summary>
    [DataField]
    public float AirborneRange = 3f;
}

[Serializable, NetSerializable]
public sealed class StageData
{
    /// <summary>
    /// The stage for the disease
    /// </summary>
    [ViewVariables]
    public int Stage = 0;

    /// <summary>
    /// The time until the disease attempts spreading.
    /// </summary>
    [ViewVariables]
    public TimeSpan MinStageUntil;
    
    /// <summary>
    /// The time until the disease attempts forceful advance
    /// </summary>
    [ViewVariables]
    public TimeSpan MaxStageUntil;
}
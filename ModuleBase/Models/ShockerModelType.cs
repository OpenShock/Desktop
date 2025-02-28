// ReSharper disable InconsistentNaming
namespace OpenShock.Desktop.ModuleBase.Models;

// TODO: Add parser in sdk that allows for unknown values to be parsed as Unknown
public enum ShockerModelType : byte
{
    CaiXianlin = 0,
    PetTrainer = 1, // Misspelled, should be "petrainer",
    Petrainer998DR = 2,
    
    /// <summary>
    /// Whenever we fail to parse the model type
    /// </summary>
    Unknown = byte.MaxValue, 
}
using System;

namespace TownOfHostForE;

public sealed class PetSelectionOptionItem : StringOptionItem
{
    // EHRの選択方式を参考に、IDを保存し、表示名はゲーム本体から取得する。
    // https://github.com/Gurge44/EndlessHostRoles/blob/19675a7434c6bd4ce59ac6da493f486c8f01a03c/Modules/OptionHolder.cs
    private static readonly string[] PetIds =
    {
        "pet_HamPet",
        "pet_Goose",
        "pet_Bedcrab",
        "pet_DancingSkeletonPet",
        "pet_BredPet",
        "pet_YuleGoatPet",
        "pet_Bush",
        "pet_Charles",
        "pet_ChewiePet",
        "pet_clank",
        "pet_coaltonpet",
        "pet_Creb",
        "pet_Cube",
        "pet_lny_dragon",
        "pet_Doggy",
        "pet_Ellie",
        "pet_Strawb",
        "pet_frankendog",
        "pet_D2GhostPet",
        "pet_test",
        "pet_GuiltySpark",
        "pet_Stickmin",
        "pet_Hamster",
        "pet_Alien",
        "pet_poro",
        "pet_Crow",
        "pet_Lava",
        "pet_Crewmate",
        "pet_Mister",
        "pet_nancy",
        "pet_napstamate",
        "pet_Pip",
        "pet_pocketCircuitCar",
        "pet_D2PoukaPet",
        "pet_Pusheen",
        "pet_Pate",
        "pet_Rammy",
        "pet_Robot",
        "pet_Snow",
        "pet_spaceCat",
        "pet_Squig",
        "pet_Stormy",
        "pet_nuggetPet",
        "pet_Charles_Red",
        "pet_UFO",
        "pet_D2WormPet",
        "AutoGrantPetRandom",
    };

    public PetSelectionOptionItem(int id)
        : base(id, "AutoGrantPetType", 0, TabGroup.MainSettings, true, PetIds) { }

    public override string GetString()
    {
        var index = GetValue();
        if (index == PetIds.Length - 1) return Translator.GetString("AutoGrantPetRandom");
        var petId = PetIds[index];
        var pet = GetPetData(petId);
        if (pet && !pet.IsEmpty && TranslationController.InstanceExists)
        {
            try
            {
                var petName = pet.GetItemName();
                if (!string.IsNullOrWhiteSpace(petName)
                    && petName.IndexOf("STRMISS", StringComparison.OrdinalIgnoreCase) < 0
                    && petName.IndexOf("STARMISS", StringComparison.OrdinalIgnoreCase) < 0
                    && petName.IndexOf("<INVALID", StringComparison.OrdinalIgnoreCase) < 0)
                    return petName;
            }
            catch
            {
                // カタログや翻訳が未準備でも、設定画面の更新を止めない。
            }
        }
        return petId;
    }

    public string GetPetId()
    {
        var index = GetValue();
        if (index == PetIds.Length - 1) index = IRandom.Instance.Next(PetIds.Length - 1);
        var petId = PetIds[index];
        var pet = GetPetData(petId);
        return pet && !pet.IsEmpty ? petId : PetIds[0];
    }

    private static PetData GetPetData(string petId)
    {
        if (!HatManager.InstanceExists) return null;
        try
        {
            return HatManager.Instance.GetPetById(petId);
        }
        catch
        {
            return null;
        }
    }
}

reagent-name-mutadon = mutadon
reagent-desc-mutadon = A medication that cures genetic diseases and resets structural enzyme blocks.
reagent-name-bromine = bromine
reagent-desc-bromine = A toxic halogen element, usually liquid at room temperature.
reagent-effect-guidebook-cure-dna-disease =
    { $chance ->
        [1] Cures a genetic disease
        *[other] Cures a genetic disease with a chance of {NATURALPERCENT($chance, 2)} each metabolism tick
    }
reagent-effect-guidebook-mutate-dna = Randomly activates disease-tier genetic blocks
job-description-geneticist = Sequence mutations at the genetics console, print mutators, and distribute useful genes responsibly.

entity-effect-guidebook-delete-entity = {$chance ->
    [1] deletes
    *[other] delete
} the target
entity-effect-guidebook-speak = Causes involuntary speech
entity-effect-guidebook-scale-entity = Scales the target's size by ({$x}, {$y})
entity-effect-guidebook-attack-self = {$chance ->
    [1] makes
    *[other] make
} the target {$useHeld ->
    [true] attack
    *[false] punch
} itself
entity-effect-guidebook-attack-others = {$chance ->
    [1] makes
    *[other] make
} the target attack a random nearby thing
entity-effect-guidebook-start-use-delay = {$chance ->
    [1] starts
    *[other] start
} the {$id} use delay on the target
entity-effect-guidebook-set-standing = {$chance ->
    [1] makes
    *[other] make
} the target {$standing ->
    [true] stand up
    *[other] get knocked down
}
entity-effect-guidebook-relay-mutated = for the mutation's host, {$effect}
entity-effect-guidebook-scramble-dna = {$chance ->
    [1] scrambles
    *[other] scramble
} the target's mutations
entity-effect-guidebook-modify-knockdown = {$chance ->
    [1] knocks
    *[other] knock
} the target down for {$time}
entity-effect-guidebook-make-felinid = Turns the target into a felinid
entity-effect-guidebook-revert-felinid = Turns the target back into a person
entity-effect-guidebook-melts-eyes = {$chance ->
    [1] melts
    *[other] melt
} the target's eyes
entity-effect-guidebook-melts-brain = {$chance ->
    [1] melts
    *[other] melt
} the target's brain
entity-effect-guidebook-increases-reach = increases reach
entity-effect-guidebook-decreases-reach = decreases reach
entity-effect-guidebook-paralyze-legs = paralyzes the target's legs
entity-effect-guidebook-make-fragile = makes the target fragile

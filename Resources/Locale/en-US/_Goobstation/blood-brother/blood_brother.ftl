roles-antag-blood-brother-name = Blood Brother
roles-antag-blood-brother-objective = Recruit your brothers one by one, complete your shared objectives and escape together.
job-name-blood-brother = Blood Brother

blood-brother-roundend-name = Blood Brother
blood-brother-round-end-count = {$count ->
    [one] There was {$count} blood brother conspiracy.
   *[other] There were {$count} blood brother conspiracies.
}
blood-brother-round-end-team = Conspiracy {$index}: {$members} — {$completed}/{$total} objectives completed.

blood-brother-role-greeting = You are a [color=crimson][bold]Blood Brother[/bold][/color]. You start alone: nobody knows who you are, and you know no other brother. Flash a brainwashable person directly with a handheld flash to recruit your first brother — he inherits every one of your objectives. A recruit rarely gets a flash of his own and can bring in a third brother; a conspiracy never grows past three. Stay in the shadows and escape together.
blood-brother-converted-greeting = [color=crimson]{$recruiter}[/color] has recruited you into the [color=crimson][bold]Blood Brothers[/bold][/color]. Your brothers: {$brothers}. Their objectives are now yours, and you win or lose together.
blood-brother-partner-list = Your brothers: [color=crimson]{$brothers}[/color]. You share every objective with them.
blood-brother-partner-converted = [color=crimson]{$name}[/color] has been converted into a blood brother.
blood-brother-partner-flash = The flash still feels warm in your hands: you can recruit one more brother of your own.

blood-brother-convert-fail-unconscious = They are in no state to be converted.
blood-brother-convert-fail-vacant = There is no mind to convert.
blood-brother-convert-fail-mindshield = Their mindshield resists the conversion!
blood-brother-convert-fail-already = They are already a blood brother.
blood-brother-convert-fail-antag = They are already loyal to someone else!
blood-brother-convert-fail-target = That is your target!
blood-brother-convert-fail-empty = You have already spent your flash and cannot recruit anyone.
blood-brother-convert-fail-full = Your conspiracy already has three brothers.

admin-verb-text-make-blood-brother = Make Blood Brother
admin-verb-make-blood-brother = Make the target a lone blood brother with his own conspiracy and his own flash.

ent-BloodBrotherConvertObjective = Convert a brother
    .desc = Flash a brainwashable person directly with a handheld flash to recruit them into your conspiracy.
ent-BloodBrotherEscapeObjective = Escape with your blood brothers
    .desc = Every member of your conspiracy must escape on the emergency shuttle alive and unrestrained. A dead or captured brother drags the objective down.

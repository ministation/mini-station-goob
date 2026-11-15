roles-antag-blood-brother-name = Кровный брат
roles-antag-blood-brother-objective = Вербуйте братьев по одному, выполните общие цели и улетайте вместе.
job-name-blood-brother = Кровный брат

blood-brother-roundend-name = Кровный брат
blood-brother-round-end-count = {$count ->
    [one] В раунде был {$count} заговор кровных братьев.
    [few] В раунде было {$count} заговора кровных братьев.
   *[other] В раунде было {$count} заговоров кровных братьев.
}
blood-brother-round-end-team = Заговор {$index}: {$members} — выполнено целей: {$completed}/{$total}.

blood-brother-role-greeting = Вы — [color=crimson][bold]Кровный брат[/bold][/color]. Вы начинаете в одиночку: никто не знает, кто вы, и вы не знаете других братьев. Направьте вспышку на подходящего человека, чтобы завербовать первого брата — он унаследует все ваши цели. Редко у завербованного оказывается своя вспышка, и тогда он может привести третьего брата; больше трёх заговор не растёт. Действуйте скрытно и улетайте вместе.
blood-brother-converted-greeting = [color=crimson]{$recruiter}[/color] завербовал вас в [color=crimson][bold]Кровные братья[/bold][/color]. Ваши братья: {$brothers}. Их цели теперь ваши, и вы побеждаете или проигрываете вместе.
blood-brother-partner-list = Ваши братья: [color=crimson]{$brothers}[/color]. Вы делите с ними все цели.
blood-brother-partner-converted = [color=crimson]{$name}[/color] обращён в кровного брата.
blood-brother-partner-flash = Вспышка ещё тёплая в ваших руках: вы можете завербовать ещё одного брата.

blood-brother-convert-fail-unconscious = Они не в том состоянии, чтобы их обращать.
blood-brother-convert-fail-vacant = Здесь нет разума, который можно обратить.
blood-brother-convert-fail-mindshield = Их щит разума сопротивляется обращению!
blood-brother-convert-fail-already = Они уже кровные братья.
blood-brother-convert-fail-antag = Они уже верны кому-то другому!
blood-brother-convert-fail-target = Это ваша цель!
blood-brother-convert-fail-empty = Вы уже потратили свою вспышку и не можете никого завербовать.
blood-brother-convert-fail-full = В вашем заговоре уже три брата.

admin-verb-text-make-blood-brother = Сделать кровным братом
admin-verb-make-blood-brother = Сделать цель одиноким кровным братом со своим заговором и своей вспышкой.

ent-BloodBrotherConvertObjective = Обратить брата
    .desc = Направьте вспышку на подходящего человека, чтобы завербовать его в свой заговор.
ent-BloodBrotherEscapeObjective = Улететь вместе с кровными братьями
    .desc = Все члены вашего заговора должны улететь на эвакуационном шаттле живыми и свободными. Мёртвый или захваченный брат тянет цель вниз.

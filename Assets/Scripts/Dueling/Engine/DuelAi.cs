using System.Collections.Generic;
using System.Linq;
using DuelGenesis.Cards;

namespace DuelGenesis.Dueling
{
    /// <summary>
    /// CPU duelist. Plays through exactly the same engine API as the human player.
    /// <see cref="TakeAction"/> performs at most ONE action per call so the table can animate
    /// each play; the controller calls it repeatedly and advances the phase when it returns false.
    /// </summary>
    public sealed class DuelAi : IDuelDecider
    {
        private readonly int _me;
        private int _bluffsSet;

        public DuelAi(int playerIndex)
        {
            _me = playerIndex;
        }

        // ============================================================== decisions (IDuelDecider)

        public List<DuelCard> ChooseCards(DuelEngine e, DuelChoice c)
        {
            if (c.IsResponseWindow)
                return ChooseResponse(e, c);

            string context = c.Context ?? string.Empty;
            IEnumerable<DuelCard> ordered = context switch
            {
                "tribute" => c.Candidates.OrderBy(card => MonsterValue(e, e.FindMonster(card))),
                "discard" => c.Candidates.OrderBy(card => HandValue(e, card)),
                "revive" => c.Candidates.OrderByDescending(card => card.Data.attack),
                "boost" => c.Candidates.OrderByDescending(card => card.Controller == _me ? 1 : 0)
                                        .ThenByDescending(card => e.GetAttack(e.FindMonster(card))),
                "destroy-backrow" => c.Candidates.OrderByDescending(card => card.Controller != _me ? 1 : 0)
                                                  .ThenByDescending(card => e.FindBackrow(card)?.FaceDown == true ? 1 : 0),
                _ => c.Candidates.OrderByDescending(card => card.Controller != _me ? 1 : 0)
                                  .ThenByDescending(card => MonsterValue(e, e.FindMonster(card)))
            };
            return ordered.Take(c.MaxCount).ToList();
        }

        public int ChooseOption(DuelEngine e, DuelChoice c) => 0;

        private List<DuelCard> ChooseResponse(DuelEngine e, DuelChoice c)
        {
            DuelCard best = null;
            int bestValue = 49;
            foreach (DuelCard card in c.Candidates)
            {
                CardEffect effect = CardEffects.Get(card.Data);
                if (effect == null) continue;
                int value = effect.AiValue(new EffectContext(e, _me, card, c.Trigger));
                if (value > bestValue)
                {
                    bestValue = value;
                    best = card;
                }
            }
            return best != null ? new List<DuelCard> { best } : new List<DuelCard>();
        }

        // ============================================================== turn play

        /// <summary>Performs one action. Returns false when there is nothing more to do this phase.</summary>
        public bool TakeAction(DuelEngine e)
        {
            if (e.IsBusy || e.TurnPlayer != _me) return false;
            switch (e.Phase)
            {
                case DuelPhase.Main1:
                    return ActivateBestSpell(e, 60) || NormalSummon(e) || ActivateBestSpell(e, 45) ||
                           FlipSummon(e) || ImprovePositions(e, attacking: true) || SetBackrow(e);
                case DuelPhase.Battle:
                    return Attack(e);
                case DuelPhase.Main2:
                    return ActivateBestSpell(e, 60) || NormalSummon(e) || ImprovePositions(e, attacking: false) || SetBackrow(e);
                default:
                    return false;
            }
        }

        public void OnTurnStart() => _bluffsSet = 0;

        private DuelistState Me(DuelEngine e) => e.Me(_me);
        private DuelistState Opp(DuelEngine e) => e.Opponent(_me);

        private int OpponentBestAttack(DuelEngine e) =>
            Opp(e).MonstersOnField.Where(m => m.IsFaceUp).Select(e.GetAttack).DefaultIfEmpty(0).Max();

        private int OpponentBestStat(DuelEngine e) =>
            Opp(e).MonstersOnField.Select(m => m.IsFaceDown ? 1500 : m.IsAttackPosition ? e.GetAttack(m) : e.GetDefense(m)).DefaultIfEmpty(0).Max();

        private bool ActivateBestSpell(DuelEngine e, int threshold)
        {
            DuelCard best = null;
            int bestValue = threshold - 1;
            IEnumerable<DuelCard> options = Me(e).Hand.Where(c => c.IsSpell)
                .Concat(Me(e).SpellTrapsOnField.Where(s => s.FaceDown).Select(s => s.Card));
            foreach (DuelCard card in options)
            {
                if (!e.CanActivate(_me, card)) continue;
                int value = CardEffects.Get(card.Data).AiValue(new EffectContext(e, _me, card, null));
                if (value > bestValue)
                {
                    bestValue = value;
                    best = card;
                }
            }
            return best != null && e.Activate(_me, best);
        }

        private bool NormalSummon(DuelEngine e)
        {
            DuelistState me = Me(e);
            if (me.NormalSummonUsed) return false;

            int threat = OpponentBestStat(e);
            int threatAttack = OpponentBestAttack(e);
            DuelCard bestCard = null;
            bool bestSet = false;
            int bestScore = int.MinValue;

            foreach (DuelCard card in me.Hand.Where(c => c.IsMonster))
            {
                if (!e.CanNormalSummon(_me, card, false)) continue;
                int tributes = DuelRules.TributesRequired(card.Data);
                int tributeCost = me.MonstersOnField.Select(m => MonsterValue(e, m)).OrderBy(v => v).Take(tributes).Sum();
                int atk = card.Data.attack;
                int def = card.Data.defense;

                bool attack = atk >= threat || Opp(e).MonsterCount == 0 || atk >= threatAttack + 300 || tributes > 0;
                int score = attack ? atk + (atk > threat ? 600 : 0) : (int)(def * 0.7f);
                score -= tributeCost;
                if (tributes > 0 && atk - tributeCost < 500) continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCard = card;
                    bestSet = !attack;
                }
            }

            if (bestCard == null) return false;
            return e.NormalSummon(_me, bestCard, bestSet);
        }

        private bool FlipSummon(DuelEngine e)
        {
            int threat = OpponentBestAttack(e);
            foreach (DuelMonsterState m in Me(e).MonstersOnField)
            {
                if (!e.CanFlipSummon(_me, m)) continue;
                if (m.Card.Data.attack > threat || Opp(e).MonsterCount == 0)
                    return e.FlipSummon(_me, m);
            }
            return false;
        }

        private bool ImprovePositions(DuelEngine e, bool attacking)
        {
            int threat = OpponentBestAttack(e);
            foreach (DuelMonsterState m in Me(e).MonstersOnField)
            {
                if (!e.CanChangePosition(_me, m)) continue;
                int atk = e.GetAttack(m);
                int def = e.GetDefense(m);
                if (attacking && m.IsDefensePosition && (atk > threat + 100 || Opp(e).MonsterCount == 0) && e.TurnNumber > 1)
                    return e.ChangePosition(_me, m);
                if (!attacking && m.IsAttackPosition && atk < threat && def > atk)
                    return e.ChangePosition(_me, m);
            }
            return false;
        }

        private bool SetBackrow(DuelEngine e)
        {
            DuelistState me = Me(e);
            if (!me.HasFreeSpellTrapZone) return false;

            // Real traps and Quick-Plays first.
            DuelCard trap = me.Hand.FirstOrDefault(c => (c.IsTrap || DuelRules.IsQuickPlay(c.Data)) && CardEffects.Get(c.Data) != null);
            if (trap != null && e.CanSetSpellTrap(_me, trap))
                return e.SetSpellTrap(_me, trap);

            // A couple of bluffs, and dump dead cards if the hand is going to be too big.
            bool handTooBig = me.Hand.Count > DuelRules.HandSizeLimit;
            DuelCard bluff = me.Hand.FirstOrDefault(c => !c.IsMonster && (CardEffects.Get(c.Data) == null || c.IsTrap));
            if (bluff != null && (_bluffsSet < 2 || handTooBig) && me.SpellTrapCount < 4 && e.CanSetSpellTrap(_me, bluff))
            {
                _bluffsSet++;
                return e.SetSpellTrap(_me, bluff);
            }
            return false;
        }

        private bool Attack(DuelEngine e)
        {
            DuelistState opp = Opp(e);
            foreach (DuelMonsterState attacker in Me(e).MonstersOnField.OrderByDescending(e.GetAttack).ToList())
            {
                if (!e.CanAttack(_me, attacker)) continue;
                int atk = e.GetAttack(attacker);

                if (opp.MonsterCount == 0)
                    return e.DeclareAttack(_me, attacker, null);

                DuelMonsterState bestTarget = null;
                int bestValue = 0;
                foreach (DuelMonsterState target in opp.MonstersOnField)
                {
                    int value;
                    if (target.IsFaceDown)
                        value = atk >= 1500 ? 600 : 0;
                    else if (target.IsAttackPosition)
                    {
                        int theirAtk = e.GetAttack(target);
                        if (atk > theirAtk) value = MonsterValue(e, target) + (atk - theirAtk);
                        else if (atk == theirAtk && MonsterValue(e, target) > MonsterValue(e, attacker)) value = 300;
                        else value = 0;
                    }
                    else
                    {
                        int def = e.GetDefense(target);
                        value = atk > def ? MonsterValue(e, target) : 0;
                    }

                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestTarget = target;
                    }
                }

                if (bestTarget != null)
                    return e.DeclareAttack(_me, attacker, bestTarget);
            }
            return false;
        }

        // ============================================================== evaluation

        private static int MonsterValue(DuelEngine e, DuelMonsterState m)
        {
            if (m == null) return 0;
            if (m.IsFaceDown) return 800 + m.Card.Data.defense / 3;
            return e.GetAttack(m) + e.GetDefense(m) / 3 + m.Level * 50;
        }

        private static int HandValue(DuelEngine e, DuelCard card)
        {
            if (card.IsMonster)
                return DuelRules.CanEverBeNormalSummoned(card.Data) ? card.Data.attack / 2 + card.Data.defense / 4 : 50;
            CardEffect effect = CardEffects.Get(card.Data);
            return effect == null ? 0 : 900;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TrafficGame.Views.UI
{
    /// <summary>
    /// Shows and hides elements through their USS transitions instead of switching them on and off. The class
    /// <see cref="HiddenClass"/> describes the hidden look (faded, moved, collapsed) in Game.uss; a hidden element stays
    /// in the hierarchy with that class and leaves the layout (display: none) once its transition has played, so
    /// showing it again animates from the hidden look.
    /// </summary>
    public static class Motion
    {
        public const string HiddenClass = "is-hidden";

        /// <summary>A label whose text is being swapped: it fades out, changes and fades back in.</summary>
        private const string SwappingClass = "is-swapping";
        private const long SwapMilliseconds = 110;

        private static readonly Dictionary<VisualElement, IVisualElementScheduledItem> Pending = new();
        private static readonly Dictionary<VisualElement, IVisualElementScheduledItem> PendingShows = new();
        private static readonly Dictionary<Label, string> PendingTexts = new();

        /// <summary>Shown or on its way in.</summary>
        public static bool IsShown(VisualElement element) =>
            PendingShows.ContainsKey(element) || (element.style.display != DisplayStyle.None && !element.ClassListContains(HiddenClass));

        public static void SetShown(VisualElement element, bool shown)
        {
            if (shown)
                Show(element);
            else
                Hide(element);
        }

        /// <remarks>
        /// The element first enters the layout with its hidden look and drops <see cref="HiddenClass"/> one panel update
        /// later: elements out of the layout keep stale styles, and the frame that shows something often does heavy
        /// work (a level being built) that would otherwise swallow the start of the animation.
        /// </remarks>
        public static void Show(VisualElement element)
        {
            if (IsShown(element) && !Pending.ContainsKey(element))
                return;

            Cancel(element);
            element.AddToClassList(HiddenClass);
            element.style.display = DisplayStyle.Flex;
            PendingShows[element] = element.schedule.Execute(() =>
            {
                PendingShows.Remove(element);
                element.RemoveFromClassList(HiddenClass);
            });
        }

        /// <param name="instantly">Skip the transition (setting up a view).</param>
        /// <param name="milliseconds">How long the hiding transition plays when <c>.is-hidden</c> sets its own duration.</param>
        public static void Hide(VisualElement element, bool instantly = false, long? milliseconds = null)
        {
            Cancel(element);
            element.AddToClassList(HiddenClass);
            if (instantly)
            {
                element.style.display = DisplayStyle.None;
                return;
            }

            if (element.style.display == DisplayStyle.None)
                return;

            var duration = milliseconds ?? TransitionMilliseconds(element);
            if (duration <= 0)
            {
                element.style.display = DisplayStyle.None;
                return;
            }

            Pending[element] = element.schedule.Execute(() =>
            {
                Pending.Remove(element);
                element.style.display = DisplayStyle.None;
            }).StartingIn(duration);
        }

        /// <summary>Changes a label's text with a quick fade through it when the label is on screen.</summary>
        public static void SetText(Label label, string text)
        {
            if (PendingTexts.ContainsKey(label))
            {
                PendingTexts[label] = text;
                return;
            }

            if (label.text == text)
                return;

            if (!IsShown(label) || string.IsNullOrEmpty(label.text) || label.panel == null)
            {
                label.text = text;
                return;
            }

            PendingTexts[label] = text;
            label.AddToClassList(SwappingClass);
            label.schedule.Execute(() =>
            {
                label.text = PendingTexts[label];
                PendingTexts.Remove(label);
                label.RemoveFromClassList(SwappingClass);
            }).StartingIn(SwapMilliseconds);
        }

        /// <summary>
        /// Plays a one-off entrance: the element starts with <paramref name="className"/> (its "before" look) and drops it
        /// after <paramref name="delayMilliseconds"/>, so it transitions into its normal look.
        /// </summary>
        public static void Enter(VisualElement element, string className, long delayMilliseconds)
        {
            element.AddToClassList(className);
            element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(Math.Max(delayMilliseconds, 16));
        }

        private static void Cancel(VisualElement element)
        {
            if (Pending.Remove(element, out var item))
                item.Pause();
            if (PendingShows.Remove(element, out var show))
                show.Pause();
        }

        /// <summary>The longest transition (duration + delay) the element's current style declares.</summary>
        private static long TransitionMilliseconds(VisualElement element)
        {
            var longest = 0f;
            var style = element.resolvedStyle;
            using var durations = style.transitionDuration.GetEnumerator();
            using var delays = style.transitionDelay.GetEnumerator();
            while (durations.MoveNext())
            {
                var delay = delays.MoveNext() ? Seconds(delays.Current) : 0f;
                longest = Math.Max(longest, Seconds(durations.Current) + delay);
            }

            return (long)Math.Ceiling(longest * 1000f);
        }

        private static float Seconds(TimeValue time) => time.unit == TimeUnit.Millisecond ? time.value / 1000f : time.value;
    }
}

using TrafficGame.Core.Construction;
using TrafficGame.Presentation.Tools;

namespace TrafficGame.Presentation.Localization
{
    /// <summary>Every localization key used by code. Tests check that each language file defines all of them.</summary>
    public static class TextKeys
    {
        public const string Balance = "hud.balance";
        public const string Cost = "tooltip.cost";
        public const string Refund = "tooltip.refund";
        public const string AddLights = "tooltip.add_lights";
        public const string ProtectedLeft = "tooltip.protected_left";
        public const string RemoveLights = "tooltip.remove_lights";
        public const string EditLanes = "tooltip.edit_lanes";
        public const string ChangeArrows = "tooltip.change_arrows";
        public const string Angle = "tooltip.angle";
        public const string Elevated = "tooltip.elevated";
        public const string Ramp = "tooltip.ramp";
        public const string ElevationLevel = "elevation.level";
        public const string Boosted = "hud.boosted";

        public const string ObjectiveTrips = "objective.trips";
        public const string ObjectiveCount = "objective.count";
        public const string ObjectiveAverage = "objective.average";
        public const string StarGoal = "star.goal";
        public const string StarSpent = "star.spent";
        public const string StarTrip = "star.trip";
        public const string StarSpentAndTrip = "star.spent_and_trip";
        public const string ResultTitle = "result.title";
        public const string ResultSpent = "result.spent";
        public const string ResultAverageTrip = "result.average_trip";
        public const string ResultTrips = "result.trips";
        public const string Money = "value.money";
        public const string Seconds = "value.seconds";
        public const string Count = "value.count";
        public const string ResultRetry = "result.retry";
        public const string ResultNext = "result.next";
        public const string ResultLevels = "result.levels";
        public const string IntroStart = "intro.start";
        public const string IntroBack = "intro.back";
        public const string IntroCaption = "intro.caption";
        public const string MenuTitle = "menu.title";
        public const string MenuSubtitle = "menu.subtitle";
        public const string MenuSandbox = "menu.sandbox";
        public const string MenuSandboxDetails = "menu.sandbox_details";
        public const string MenuContinue = "menu.continue";
        public const string MenuCampaign = "menu.campaign";
        public const string MenuStars = "menu.stars";
        public const string MenuNew = "menu.new";
        public const string MenuLevel = "menu.level";
        public const string SandboxTitle = "level.sandbox.title";

        public const string HintNone = "hint.none";
        public const string HintRoad = "hint.road";
        public const string HintCurvedRoad = "hint.curved_road";
        public const string HintRoundabout = "hint.roundabout";
        public const string HintBulldoze = "hint.bulldoze";
        public const string HintTrafficLights = "hint.traffic_lights";
        public const string HintLaneArrows = "hint.lane_arrows";

        public const string ButtonRoad = "button.road";
        public const string ButtonCurvedRoad = "button.curved_road";
        public const string ButtonRoundabout = "button.roundabout";
        public const string ButtonBulldoze = "button.bulldoze";
        public const string ButtonTrafficLights = "button.traffic_lights";
        public const string ButtonLaneArrows = "button.lane_arrows";
        public const string ButtonUndo = "button.undo";
        public const string ButtonRedo = "button.redo";
        public const string ButtonBack = "button.back";
        public const string ButtonConfirm = "button.confirm";

        public const string ProfileDetails = "profile.details";
        public const string ProfileDetailsOneWay = "profile.details_one_way";

        public const string GuideContinuation = "guide.continuation";
        public const string GuideParallel = "guide.parallel";
        public const string GuidePerpendicular = "guide.perpendicular";

        public const string ErrorTooShort = "error.too_short";
        public const string ErrorCurveTooSharp = "error.curve_too_sharp";
        public const string ErrorAngleTooSharp = "error.angle_too_sharp";
        public const string ErrorJunctionTooClose = "error.junction_too_close";
        public const string ErrorOverlapsRoad = "error.overlaps_road";
        public const string ErrorOutOfBounds = "error.out_of_bounds";
        public const string ErrorInsufficientFunds = "error.insufficient_funds";
        public const string ErrorTooSteep = "error.too_steep";
        public const string ErrorHeightConflict = "error.height_conflict";
        public const string ErrorLocked = "error.locked";
        public const string ErrorRoundaboutLights = "error.roundabout_lights";

        /// <summary>Key of a road profile's display name.</summary>
        public static string Profile(string profileId) => "profile." + profileId;

        public static string Hint(ToolKind tool) => tool switch
        {
            ToolKind.Road => HintRoad,
            ToolKind.CurvedRoad => HintCurvedRoad,
            ToolKind.Roundabout => HintRoundabout,
            ToolKind.Bulldoze => HintBulldoze,
            ToolKind.TrafficLights => HintTrafficLights,
            ToolKind.LaneArrows => HintLaneArrows,
            _ => HintNone
        };

        public static string Guide(GuideKind guide) => guide switch
        {
            GuideKind.Continuation => GuideContinuation,
            GuideKind.Parallel => GuideParallel,
            GuideKind.Perpendicular => GuidePerpendicular,
            _ => null
        };

        public static string Error(BuildError error) => error switch
        {
            BuildError.TooShort => ErrorTooShort,
            BuildError.CurveTooSharp => ErrorCurveTooSharp,
            BuildError.AngleTooSharp => ErrorAngleTooSharp,
            BuildError.JunctionTooClose => ErrorJunctionTooClose,
            BuildError.OverlapsRoad => ErrorOverlapsRoad,
            BuildError.OutOfBounds => ErrorOutOfBounds,
            BuildError.InsufficientFunds => ErrorInsufficientFunds,
            BuildError.TooSteep => ErrorTooSteep,
            BuildError.HeightConflict => ErrorHeightConflict,
            _ => null
        };
    }
}

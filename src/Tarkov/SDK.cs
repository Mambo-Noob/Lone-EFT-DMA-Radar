using System.Linq;
using static LoneEftDmaRadar.Tarkov.Unity.UnitySDK;

namespace SDK
{
    public readonly partial struct Offsets
    {
        public readonly partial struct GameWorld
        {
            public const uint BtrController = 0x20; // EFT.Vehicle.BtrController
            public const uint LocationId = 0xB8; // string
            public const uint Location = 0xB8; // String (alias)
            public const uint LootList = 0x178; // System.Collections.Generic.List<IKillable>
            public const uint RegisteredPlayers = 0x190; // System.Collections.Generic.List<IPlayer>
            public const uint MainPlayer = 0x1E0; // EFT.Player
            public const uint SynchronizableObjectLogicProcessor = 0x218; // EFT.SynchronizableObjects.SynchronizableObjectLogicProcessor
            public const uint Grenades = 0x258; // DictionaryListHydra<int, Throwable>
        }

        public readonly partial struct ClientLocalGameWorld
        {
            public const uint BtrController = 0x50; // -.\uF07E
            public const uint LootList = 0x178; // System.Collections.Generic.List<\uE311>
            public const uint RegisteredPlayers = 0x190; // System.Collections.Generic.List<IPlayer>
            public const uint MainPlayer = 0x1E0; // EFT.Player
            public const uint SynchronizableObjectLogicProcessor = 0x218; // -.\uEBD9
            public const uint Grenades = 0x258; // -.\uE3D7<Int32, Throwable>
        }

		public readonly partial struct ProceduralWeaponAnimation //Class: EFT.Animations.ProceduralWeaponAnimation ---
		{
			public const uint HandsContainer = 0x20; // EFT.Animations.PlayerSpring <HandsContainer> HandsContainer
			public const uint Breath = 0x38; // EFT.Animations.BreathEffector <Breath> Breath
			public const uint MotionReact = 0x48; // -.MotionEffector <MotionReact> MotionReact
			public const uint Shootingg = 0x58; // -.ShotEffector <Shootingg> Shootingg
			public const uint _optics = 0x180; // System.Collections.Generic.List<ProceduralWeaponAnimation.SightNBone>
			public const uint Mask = 0x30; // System.Int32 <Mask> Mask
			public const uint IsAiming = 0x145; // Boolean
			public const uint _isAiming = 0x145; // Boolean <_isAiming> _isAiming
			public const uint _fieldOfView = 0xA8; // Float
			public const uint _aimingSpeed = 0x164; // Single <_aimingSpeed> _aimingSpeed
			public const uint _fovCompensatoryDistance = 0x194; // Single <_fovCompensatoryDistance> _fovCompensatoryDistance
			public const uint _compensatoryScale = 0x1c4; // Single <_compensatoryScale> _compensatoryScale
			public const uint _shotDirection = 0x1c8; // UnityEngine.Vector3 <_shotDirection> _shotDirection
			public const uint CameraSmoothOut = 0x20c; // Single <CameraSmoothOut> CameraSmoothOut
			public const uint PositionZeroSum = 0x31c; // UnityEngine.Vector3 <PositionZeroSum> PositionZeroSum
			public const uint ShotNeedsFovAdjustments = 0x433; // Boolean <<ShotNeedsFovAdjustments>k__BackingField> <ShotNeedsFovAdjustments>k__BackingField
		}

        public readonly partial struct OpticCameraManagerContainer
        {
            public const uint Instance = 0x0; // -.\uF124
            public const uint OpticCameraManager = 0x10; // -.\uF125
            public const uint FPSCamera = 0x60; // UnityEngine.Camera
        }

        public readonly partial struct OpticCameraManager
        {
            public const uint Camera = 0x70; // UnityEngine.Camera
            public const uint CurrentOpticSight = 0x78; // EFT.CameraControl.OpticSight
        }

        public readonly partial struct SightNBone
        {
            public const uint Mod = 0x10; // EFT.InventoryLogic.SightComponent
        }

        public readonly partial struct SightComponent
        {
            public const uint _template = 0x20; // EFT.InventoryLogic.ISightComponentTemplate
            public const uint ScopesSelectedModes = 0x30; // System.Int32[]
            public const uint SelectedScope = 0x38; // System.Int32
            public const uint ScopeZoomValue = 0x3C; // System.Single
        }

        public readonly partial struct SightInterface
        {
            public const uint Zooms = 0x1B8; // System.Single[][]
        }

        public readonly partial struct TransitController
        {
            public const uint TransitPoints = 0x80; // System.Collections.Generic.Dictionary<Int32, TransitPoint>
        }

        public readonly partial struct TransitPoint
        {
            public const uint parameters = 0x20; // -.\uE6CC.Location.TransitParameters
        }

        public readonly partial struct TransitParameters
        {
            public const uint name = 0x18; // String
            public const uint description = 0x20; // String
            public const uint location = 0x40; // String
        }

        public readonly partial struct SynchronizableObject
        {
            public const uint Type = 0x68; // System.Int32
        }

        public readonly partial struct SynchronizableObjectLogicProcessor
        {
            public const uint SynchronizableObjects = 0x18; // System.Collections.Generic.List<SynchronizableObject>
        }

        public readonly partial struct TripwireSynchronizableObject
        {
            public const uint _tripwireState = 0xE4; // System.Int32
            public const uint ToPosition = 0x16C; // UnityEngine.Vector3
        }

        public readonly partial struct BtrController
        {
            public const uint BtrView = 0x50; // EFT.Vehicle.BTRView
        }

        public readonly partial struct BTRView
        {
            public const uint turret = 0x60; // EFT.Vehicle.BTRTurretView
            public const uint _targetPosition = 0xAC; // UnityEngine.Vector3
        }

        public readonly partial struct BTRTurretView
        {
            public const uint AttachedBot = 0x60; // System.ValueTuple<ObservedPlayerView, Boolean>
        }

        public readonly partial struct Grenade
        {
            public const uint IsDestroyed = 0x4D; // Boolean
        }

        public readonly partial struct Throwable
        {
            public const uint _isDestroyed = 0x4D; // Boolean
        }

        public readonly partial struct Player
        {
            public const uint MovementContext = 0x60; // EFT.MovementContext
            public const uint _playerBody = 0x190; // EFT.PlayerBody
            public const uint Physical = 0x8F0; // -.\uE399 <Physical> Physical
            public const uint Corpse = 0x670; // EFT.Interactive.Corpse
            public const uint Location = 0x860; // String
            public const uint Profile = 0x8D8; // EFT.Profile
            public const uint ProceduralWeaponAnimation = 0x330; // EFT.Animations.ProceduralWeaponAnimation
            public const uint _inventoryController = 0x950; // EFT.PlayerInventoryController update
            public const uint _handsController = 0x958; // EFT.PlayerHands update
        }

        public readonly partial struct ObservedPlayerView
        {
            public const uint ObservedPlayerController = 0x20; // EFT.NextObservedPlayer.ObservedPlayerController
            public const uint Voice = 0x38; // string
            public const uint GroupID = 0x78; // string
            public const uint Side = 0x8C; // EFT.EPlayerSide
            public const uint IsAI = 0x98; // bool
            public const uint AccountId = 0xB0; // string
            public const uint PlayerBody = 0xC8; // EFT.PlayerBody
        }

        public readonly partial struct ObservedPlayerController
        {
            public const uint Player = 0x18; // EFT.NextObservedPlayer.ObservedPlayerView
            public const uint MovementController = 0xD8; // -.\uED4F
            public const uint HealthController = 0xE8; // -.\uE446
            public const uint InventoryController = 0x10; // -.\uED5B update
            public const uint HandsController = 0x120; // EFT.Animations.PlayerSpring
        }

        public readonly partial struct ObservedMovementController
        {
            public const uint ObservedPlayerStateContext = 0x98;
            public const uint Rotation = 0x1c; // UnityEngine.Vector2 <HeadRotation> HeadRotation
            public const uint Velocity = 0x30; // UnityEngine.Vector3 <Velocity> Velocity
        }

        public readonly partial struct ObservedPlayerStateContext
        {
            public const uint Rotation = 0x20; // UnityEngine.Vector2
        }

        public readonly partial struct ObservedHealthController
        {
            public const uint Player = 0x18; // EFT.NextObservedPlayer.ObservedPlayerView
            public const uint PlayerCorpse = 0x20; // EFT.Interactive.ObservedCorpse
            public const uint HealthStatus = 0x10; // System.Int32
        }

        public readonly partial struct Profile
        {
            public const uint Id = 0x10; // String
            public const uint AccountId = 0x18; // String
            public const uint Info = 0x48; // -.\uE9AD
        }

        public readonly partial struct PlayerInfo
        {
            public const uint EntryPoint = 0x28; // String
            public const uint GroupId = 0x50; // String
            public const uint Side = 0x48; // [HUMAN] Int32
            public const uint RegistrationDate = 0x4C; // Int32
        }

        public readonly partial struct MovementContext
        {
			public const uint Player = 0x48; // EFT.Player
			public const uint _rotation = 0xC4; // UnityEngine.Vector2
			public const uint PlantState = 0x78; // EFT.BaseMovementState <PlantState> PlantState
			public const uint CurrentState = 0x1F0; // EFT.BaseMovementState <CurrentState>k__BackingField
			public const uint _states = 0x480; // System.Collections.Generic.Dictionary<Byte, BaseMovementState> <_states> _states
			public const uint _movementStates = 0x4b0; // -.IPlayerStateContainerBehaviour[] <_movementStates> _movementStates
			public const uint _tilt = 0xb0; // Single <_tilt> _tilt
			public const uint _physicalCondition = 0x198; // System.Int32 <_physicalCondition> _physicalCondition
			public const uint _speedLimitIsDirty = 0x1b9; // Boolean <_speedLimitIsDirty> _speedLimitIsDirty
			public const uint StateSpeedLimit = 0x1bc; // Single <<StateSpeedLimit>k__BackingField> <StateSpeedLimit>k__BackingField
			public const uint StateSprintSpeedLimit = 0x1c0; // Single <<StateSprintSpeedLimit>k__BackingField> <StateSprintSpeedLimit>k__BackingField
			public const uint _lookDirection = 0x3b8; // UnityEngine.Vector3  <_lookDirection> _lookDirection
			public const uint WalkInertia = 0x4bc; // Single <<WalkInertia>k__BackingField> <WalkInertia>k__BackingField
			public const uint SprintBrakeInertia = 0x4c0; // Single <<SprintBrakeInertia>k__BackingField> <SprintBrakeInertia>k__BackingField
        }

        public readonly partial struct MovementState //Class: EFT.MovementState ---
        {
            public const uint StickToGround = 0x54; // Boolean <StickToGround> StickToGround
            public const uint PlantTime = 0x58; // Single <PlantTime> PlantTime

            public const uint Name = 0x11; // System.Byte <Name> Name
            public const uint AnimatorStateHash = 0x18; // Int32 <AnimatorStateName> AnimatorStateName
        }

        public readonly partial struct PlayerStateContainer //Class: .PlayerStateContainer ---
        {
            public const uint Name = 0x19; // System.Byte
            public const uint StateFullNameHash = 0x40; // Int32 <StateFullNameHash> StateFullNameHash
        }

        public readonly partial struct InteractiveLootItem
        {
            public const uint Item = 0xF0; // EFT.InventoryLogic.Item
        }

        public readonly partial struct DizSkinningSkeleton
        {
            public const uint _values = 0x30; // System.Collections.Generic.List<Transform>
        }

        public readonly partial struct LootableContainer
        {
            public const uint ItemOwner = 0x168; // -.\uEFB4
        }

        public readonly partial struct LootableContainerItemOwner
        {
            public const uint RootItem = 0xD0; // EFT.InventoryLogic.Item
        }

        public readonly partial struct LootItem
        {
            public const uint Template = 0x60; // EFT.InventoryLogic.ItemTemplate
            public const uint Version = 0x150; // Int32 update
        }

        public readonly partial struct ItemTemplate
        {
            public const uint ShortName = 0x18; // String
            public const uint _id = 0xE0; // EFT.MongoID
            public const uint QuestItem = 0x34; // Boolean
        }

        public readonly partial struct PlayerBody
        {
            public const uint SkeletonRootJoint = 0x30; // Diz.Skinning.Skeleton
        }

		public readonly partial struct FirearmController //Class: .FirearmController ---
		{
			public const uint WeaponAnimation = 0x198; // EFT.Animations.ProceduralWeaponAnimation
			public const uint Fireport = 0x150; // EFT.BifacialTransform <Fireport> Fireport
			public const uint TotalCenterOfImpact = 0x2a0; // Single
            public static readonly uint[] To_FirePortTransformInternal = new uint[] { Fireport, 0x10, 0x10 };
            public static readonly uint[] To_FirePortVertices = To_FirePortTransformInternal.Concat(new uint[] { UnityOffsets.TransformInternal_TransformAccessOffset, UnityOffsets.Hierarchy_VerticesOffset }).ToArray();
		}

		public readonly partial struct ItemHandsController //Class: .ItemHandsController ---
		{
			public const uint Item = 0x70; // EFT.InventoryLogic.Item
		}

		public readonly partial struct LootItemWeapon //Class: EFT.InventoryLogic.Weapon ---
		{
			public const uint FireMode = 0xa0; // EFT.InventoryLogic.FireModeComponent <FireMode> FireMode
			public const uint Chambers = 0xb0; // EFT.InventoryLogic.Slot[] <<Chambers>k__BackingField> <Chambers>k__BackingField
			public const uint _magSlotCache = 0xc8; // EFT.InventoryLogic.Slot <_magSlotCache> _magSlotCache
		}

		public readonly partial struct LootItemMagazine //Class: EFT.InventoryLogic.MagazineTemplate ---
		{
			public const uint Cartridges = 0x1a8; // EFT.InventoryLogic.StackSlot <Cartridges> Cartridges
			public const uint LoadUnloadModifier = 0x1b0; // Single <LoadUnloadModifier> LoadUnloadModifier
		}

		public readonly partial struct StackSlot //Class: EFT.InventoryLogic.StackSlot ---
		{
			public const uint _items = 0x10; // System.Collections.Generic.List<Item>
			public const uint MaxCount = 0x38; // Int32
		}

		public readonly partial struct MagazineClass // ???
		{
			public const uint StackObjectsCount = 0x64; // Int32
		}

		public readonly partial struct Slot //Class: EFT.InventoryLogic.Slot ---
		{
			public const uint ContainedItem = 0x48; // EFT.InventoryLogic.Item
			public const uint ID = 0x58; // String
			public const uint Required = 0x18; // Boolean
		}

		public readonly partial struct LootItemMod //Class: EFT.InventoryLogic.Mod
		{
			public const uint Grids = 0x78; // -.\uEE74[]
			public const uint Slots = 0x80; // EFT.InventoryLogic.Slot[]
		}

		public readonly partial struct SlotViewsContainer // ???
		{
			public const uint Dict = 0x10; // System.Collections.Generic.Dictionary<Var, Var>
		}

		public readonly partial struct ModTemplate //Class: EFT.InventoryLogic.ModTemplate ---
		{
			public const uint Velocity = 0x188; // Single <Velocity> Velocity
		}

		public readonly partial struct AmmoTemplate //Class: EFT.InventoryLogic.AmmoTemplate ---
		{
			public const uint InitialSpeed = 0x1a4; // Single <InitialSpeed> InitialSpeed
			public const uint BallisticCoeficient = 0x1b8; // Single <BallisticCoeficient> BallisticCoeficient
			public const uint BulletMassGram = 0x25c; // Single <BulletMassGram> BulletMassGram
			public const uint BulletDiameterMilimeters = 0x260; // Single <BulletDiameterMilimeters> BulletDiameterMilimeters
		}

		public readonly partial struct WeaponTemplate //Class: EFT.InventoryLogic.WeaponTemplate ---
		{
			public const uint Velocity = 0x254; // Single <Velocity> Velocity
		}

		public readonly partial struct BreathEffector //Class: EFT.Animations.BreathEffector ---
		{
			public const uint Intensity = 0x30; // Single <Intensity> Intensity
		}

		public readonly partial struct ShotEffector //Class: .ShotEffector ---
		{
			public const uint NewShotRecoil = 0x20; // EFT.Animations.NewRecoil.NewRecoilShotEffect <NewShotRecoil> NewShotRecoil
		}

		public readonly partial struct NewShotRecoil //Class: EFT.Animations.NewRecoil.NewRecoilShotEffect ---
		{
			public const uint IntensitySeparateFactors = 0x94; // UnityEngine.Vector3 <IntensitySeparateFactors> IntensitySeparateFactors
		}

		public readonly partial struct PhysicalValue //Class: .Stamina
		{
			public const uint Current = 0x10; // Single
		}

		public readonly partial struct Physical //Class: .PhysicalBase ---
		{
			public const uint Stamina = 0x68; // -.\uE398 <Stamina> Stamina
			public const uint HandsStamina = 0x70; // -.\uE398 <HandsStamina> HandsStamina
			public const uint Oxygen = 0x78; // -.\uE398 <Oxygen> Oxygen
			public const uint Overweight = 0x1c; // Single <Overweight> Overweight
			public const uint WalkOverweight = 0x20; // Single <WalkOverweight> WalkOverweight
			public const uint WalkSpeedLimit = 0x24; // Single <WalkSpeedLimit> WalkSpeedLimit
			public const uint Inertia = 0x28; // Single <Inertia> Inertia
			public const uint WalkOverweightLimits = 0xa4; // UnityEngine.Vector2 <WalkOverweightLimits> WalkOverweightLimits
			public const uint BaseOverweightLimits = 0xac; // UnityEngine.Vector2 <BaseOverweightLimits> BaseOverweightLimits
			public const uint SprintOverweightLimits = 0xc0; // UnityEngine.Vector2 <SprintOverweightLimits> SprintOverweightLimits
			public const uint SprintWeightFactor = 0x104; // Single
			public const uint SprintAcceleration = 0x114; // Single <SprintAcceleration> SprintAcceleration
			public const uint PreSprintAcceleration = 0x118; // Single <PreSprintAcceleration> PreSprintAcceleration
			public const uint IsOverweightA = 0x11C; // Boolean
			public const uint IsOverweightB = 0x11D; // Boolean
		}

		public readonly partial struct FireModeComponent //Class: EFT.InventoryLogic.FireModeComponent ---
		{
			public const uint FireMode = 0x28; // System.Byte <FireMode> FireMode
		}
    }

    public readonly partial struct Enums
    {
        public enum EPlayerSide
        {
            Usec = 1,
            Bear = 2,
            Savage = 4,
        }

        public enum EPlayerState : byte
        {
            None = 0,
            Idle = 1,
            ProneIdle = 2,
            ProneMove = 3,
            Run = 4,
            Sprint = 5,
            Jump = 6,
            FallDown = 7,
            Transition = 8,
            BreachDoor = 9,
            Loot = 10,
            Pickup = 11,
            Open = 12,
            Close = 13,
            Unlock = 14,
            Sidestep = 15,
            DoorInteraction = 16,
            Approach = 17,
            Prone2Stand = 18,
            Transit2Prone = 19,
            Plant = 20,
            Stationary = 21,
            Roll = 22,
            JumpLanding = 23,
            ClimbOver = 24,
            ClimbUp = 25,
            VaultingFallDown = 26,
            VaultingLanding = 27,
            BlindFire = 28,
            IdleWeaponMounting = 29,
            IdleZombieState = 30,
            MoveZombieState = 31,
            TurnZombieState = 32,
            StartMoveZombieState = 33,
            EndMoveZombieState = 34,
            DoorInteractionZombieState = 35,
        }

        [Flags]
        public enum ETagStatus
        {
            Unaware = 1,
            Aware = 2,
            Combat = 4,
            Solo = 8,
            Coop = 16,
            Bear = 32,
            Usec = 64,
            Scav = 128,
            TargetSolo = 256,
            TargetMultiple = 512,
            Healthy = 1024,
            Injured = 2048,
            BadlyInjured = 4096,
            Dying = 8192,
            Birdeye = 16384,
            Knight = 32768,
            BigPipe = 65536,
            BlackDivision = 131072,
            VSRF = 262144
        }

        [Flags]
        public enum EMemberCategory
        {
            Default = 0,
            Developer = 1,
            UniqueId = 2,
            Trader = 4,
            Group = 8,
            System = 16,
            ChatModerator = 32,
            ChatModeratorWithPermanentBan = 64,
            UnitTest = 128,
            Sherpa = 256,
            Emissary = 512,
            Unheard = 1024,
        }

        public enum WildSpawnType
        {
            marksman = 0,
            assault = 1,
            bossTest = 2,
            bossBully = 3,
            followerTest = 4,
            followerBully = 5,
            bossKilla = 6,
            bossKojaniy = 7,
            followerKojaniy = 8,
            pmcBot = 9,
            cursedAssault = 10,
            bossGluhar = 11,
            followerGluharAssault = 12,
            followerGluharSecurity = 13,
            followerGluharScout = 14,
            followerGluharSnipe = 15,
            followerSanitar = 16,
            bossSanitar = 17,
            test = 18,
            assaultGroup = 19,
            sectantWarrior = 20,
            sectantPriest = 21,
            bossTagilla = 22,
            followerTagilla = 23,
            exUsec = 24,
            gifter = 25,
            bossKnight = 26,
            followerBigPipe = 27,
            followerBirdEye = 28,
            bossZryachiy = 29,
            followerZryachiy = 30,
            bossBoar = 32,
            followerBoar = 33,
            arenaFighter = 34,
            arenaFighterEvent = 35,
            bossBoarSniper = 36,
            crazyAssaultEvent = 37,
            peacefullZryachiyEvent = 38,
            sectactPriestEvent = 39,
            ravangeZryachiyEvent = 40,
            followerBoarClose1 = 41,
            followerBoarClose2 = 42,
            bossKolontay = 43,
            followerKolontayAssault = 44,
            followerKolontaySecurity = 45,
            shooterBTR = 46,
            bossPartisan = 47,
            spiritWinter = 48,
            spiritSpring = 49,
            peacemaker = 50,
            pmcBEAR = 51,
            pmcUSEC = 52,
            skier = 53,
            sectantPredvestnik = 57,
            sectantPrizrak = 58,
            sectantOni = 59,
            infectedAssault = 60,
            infectedPmc = 61,
            infectedCivil = 62,
            infectedLaborant = 63,
            infectedTagilla = 64,
            bossTagillaAgro = 65,
            bossKillaAgro = 66,
            tagillaHelperAgro = 67,
        }

        public enum EExfiltrationStatus
        {
            NotPresent = 1,
            UncompleteRequirements = 2,
            Countdown = 3,
            RegularMode = 4,
            Pending = 5,
            AwaitsManualActivation = 6,
            Hidden = 7,
        }

        public enum SynchronizableObjectType
        {
            AirDrop = 0,
            AirPlane = 1,
            Tripwire = 2,
        }

        public enum ETripwireState
        {
            None = 0,
            Wait = 1,
            Active = 2,
            Exploding = 3,
            Exploded = 4,
            Inert = 5,
        }

        public enum EQuestStatus
        {
            Locked = 0,
            AvailableForStart = 1,
            Started = 2,
            AvailableForFinish = 3,
            Success = 4,
            Fail = 5,
            FailRestartable = 6,
            MarkedAsFailed = 7,
            Expired = 8,
            AvailableAfter = 9,
        }
    }
}

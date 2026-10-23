using Godot;

using System.Collections.Generic;
using System.Linq;

public partial class RtsUnit : CharacterBody3D
{
	public enum ArtilleryDeployState
	{
		Mobile,
		Deploying,
		Deployed,
		Undeploying
	}
	public const float BomberMinBombingRunLength = 10f;
	public const float BomberMaxBombingRunLength = 24f;
	public const float BomberBombingRunLaneSpacing = 4f;
	public const float DefaultBattleFuelSeconds = 30f;
	public const float DefaultLowFuelReturnRatio = 0.25f;

	sealed class RuntimeTechBuff
	{
		public string SourceId { get; set; } = "";
		public float Remaining { get; set; }
		public float MoveMultiplier { get; set; } = 1f;
		public float DamageMultiplier { get; set; } = 1f;
		public float AttackRangeBonus { get; set; }
		public float AttackCooldownMultiplier { get; set; } = 1f;
		public float DefenseReduction { get; set; }
		public float VisionBonus { get; set; }
		public float RegenPerSecond { get; set; }
		public Color Tint { get; set; } = Colors.White;
	}

	enum InfantryVisualPartKind
	{
		Body,
		LeftLeg,
		RightLeg,
		LeftArm,
		RightArm
	}

	sealed class AnimatedVisualPart
	{
		public Node3D Node { get; init; } = null!;
		public Vector3 RestPosition { get; init; }
		public Vector3 RestRotation { get; init; }
		public float Phase { get; init; }
		public InfantryVisualPartKind Kind { get; init; }
	}

	[Signal]
	public delegate void DiedEventHandler(RtsUnit unit);

	public const uint ForestCollisionLayer = 4u;

	public static readonly List<RtsUnit> AllActiveUnits = new(128);

	public float CollisionRadius => BattleUnitCatalog.CollisionRadius(UnitKey);
	public float UnitMass => BattleUnitCatalog.UnitMass(UnitKey);

	public float Shield { get; private set; }
	public float MaxShield { get; private set; }
	float secondsSinceLastDamage = 99f;
	float grenadeCooldownLeft = 0f;

	[Export] public string UnitKey { get; set; } = "tank";
	[Export] public string DisplayName { get; set; } = "Unit";
	bool playerOwned = true;
	[Export]
	public bool PlayerOwned
	{
		get => playerOwned;
		set
		{
			playerOwned = value;
			FogRevealed = value;
			FogExplored = value;
		}
	}
	[Export] public float MaxHealth { get; set; } = 100f;
	[Export] public float MoveSpeed { get; set; } = 6f;
	[Export] public float TurnSpeed { get; set; } = 9f;
	[Export] public float AttackDamage { get; set; } = 10f;
	[Export] public float AttackRange { get; set; } = 6f;
	[Export] public float AttackCooldown { get; set; } = 1f;
	[Export] public float SplashRadius { get; set; }
	[Export] public float SplashFalloff { get; set; } = 0.5f;
	[Export] public int GoldCost { get; set; } = 100;
	[Export] public int PopCost { get; set; } = 1;
	[Export] public float CruiseHeight { get; set; }

	public int NetId { get; set; }
	public float Health { get; set; }
	public Vector3 TargetPosition { get; set; }
	public Node3D? AttackTarget { get; private set; }
	public bool Selected { get; private set; }
	public bool IsDead => Health <= 0f;
	public bool AttackMoving { get; private set; }
	public bool AttackGrounding { get; private set; }
	public bool Patrolling { get; private set; }
	public bool Guarding { get; private set; }
	public bool CanAttackGroundPoint => (UnitKey == "bomber" || !BattleUnitCatalog.IsAirUnit(UnitKey)) && SplashRadius > 0.05f && AttackRange > 0.5f && AttackDamage > 0f;
	public bool CanFireWhileMoving => !SupportsArtilleryDeploy && (BattleUnitCatalog.CanFireWhileMoving(UnitKey) || weaponYawNode is not null);
	public bool FogRevealed { get; private set; }
	/// <summary>曾经被玩家视野探索过（战争迷雾记忆）。</summary>
	public bool FogExplored { get; private set; }
	public float VisionBonus { get; private set; }
	public bool SupportsBombingRun => UnitKey == "bomber" && !IsDead;
	public bool SupportsAirfieldParking => BattleUnitCatalog.IsAirUnit(UnitKey) && !IsDead;
	public bool SupportsArtilleryDeploy => BattleUnitCatalog.SupportsArtilleryDeploy(UnitKey) && !IsDead;
	public ArtilleryDeployState DeployState { get; private set; } = ArtilleryDeployState.Mobile;
	public float DeployProgress { get; private set; } = 0f;
	public bool IsDeployed => DeployState == ArtilleryDeployState.Deployed;
	public bool IsDeployingOrUndeploying => DeployState is ArtilleryDeployState.Deploying or ArtilleryDeployState.Undeploying;
	public float CurrentFuel { get; set; }
	public float AirFuelSeconds => CurrentFuel;
	public int AirAmmoCount => CurrentAmmo;
	public void ConsumeAirFuel(float amount) => CurrentFuel = Mathf.Max(0f, CurrentFuel - amount);
	public void DeductAirAmmo(int amount = 1) => CurrentAmmo = Mathf.Max(0, CurrentAmmo - amount);
	public float FuelRatio => maxFuelSeconds <= 0.01f ? 1f : Mathf.Clamp(CurrentFuel / maxFuelSeconds, 0f, 1f);
	public bool IsReturningToRefuel => parkingAtAirfield;
	public bool IsReturningToPark => parkingAtAirfield;
	public bool IsParkedAtAirfield => parkedAtAirfield;

	const float BomberApproachLead = 9f;
	const float BomberExitLead = 7f;
	const float BomberArrivalThreshold = 1.25f;
	const float BomberFinishThreshold = 1.5f;
	const float BomberReleaseProgress = 0.58f;
	const float BomberReleaseSpacingForward = 2.2f;
	const float BomberReleaseSpacingSide = 2.0f;
	const float BomberDamageMultiplier = 0.58f;
	const float BomberImpactJitter = 0.55f;
	const float AirfieldParkingHeight = 0.72f;
	const float ReturnFuelReserveSeconds = 2f;
	float cooldownLeft;
	float acquireTimer;
	float baseMaxHealth;
	float baseMoveSpeed;
	float baseAttackDamage;
	float baseAttackRange;
	float baseAttackCooldown;
	float baseSplashRadius;
	float baseSplashFalloff;
	float techDamageReduction;
	float techRegenPerSecond;
	float techRegenCarry;
	float maxFuelSeconds;
	float lowFuelReturnRatio;
	float refuelSeconds;
	float attackSlotAngleOffset;
	Vector3 attackGroundTarget;
	Vector3 patrolA;
	Vector3 patrolB;
	bool patrolHeadingToB;
	bool isManualMoveOrder;
	RtsUnit? guardTarget;
	uint originalCollisionLayer;
	uint originalCollisionMask;
	MeshInstance3D? selectionRing;
	MeshInstance3D? techRing;
	MeshInstance3D? groundShadowRing;
	MeshInstance3D? headingNotch;
	Node3D? visualRoot;
	Vector3 currentNormal = Vector3.Up;
	Node3D? weaponYawNode;
	Node3D? weaponPitchNode;
	public Node3D? WeaponYawNode => weaponYawNode;
	public Node3D? WeaponPitchNode => weaponPitchNode;
	bool weaponPitchIsPivot;
	Vector3 weaponYawLookOffset;
	Vector3 weaponPitchLookOffset;
	Vector3 weaponPitchPivotOffset;
	AnimationPlayer? visualAnimationPlayer;
	readonly List<AnimationPlayer> visualAnimationPlayers = new();
	string walkAnimationName = "";
	string runAnimationName = "";
	string idleAnimationName = "";
	string fireAnimationName = "";
	string hitAnimationName = "";
	string reloadAnimationName = "";
	string grenadeAnimationName = "";
	string currentVisualAnimationName = "";
	float visualAnimationLockRemaining;
	float crouchRatio;
	float infantryFootstepTimer;
	int infantryFootstepSide;
	float infantryRecoil;
	bool isDying;
	float deathTimer;
	float deathDurationSeconds = 2.4f;
	bool isAirCrashing;
	Vector3 airCrashVelocity;
	CpuParticles3D? airCrashSmoke;
	CpuParticles3D? airCrashFire;

	// === 军种专属超写实殉爆与残骸系统 (Realistic Death & Destruction Wreckage System) ===
	bool isNavalSinking;
	float navalSinkPitchRate;
	float navalSinkRollRate;
	float navalSinkDepthSpeed;
	int navalSecondaryExplosionsFired;
	CpuParticles3D? navalSinkBubbles;
	CpuParticles3D? navalSinkSmoke;

	bool isGroundWreckage;
	bool turretEjected;
	Vector3 turretVelocity;
	Vector3 turretRotVelocity;
	CpuParticles3D? wreckageFire;
	CpuParticles3D? wreckageSmoke;
	MeshInstance3D? groundScorchDecal;

	bool isHelicopterCrashing;
	float heliCrashYawVelocity;
	float weaponRecoilOffset;
	float weaponRecoilVelocity;
	float chassisRecoilPitch;
	float chassisRecoilPitchVel;
	Vector3 weaponPitchRestPosition;
	bool hasRecordedWeaponPitchRest;
	Vector3 visualRootRestPosition;
	float hitFlashTimer;
	Vector3 hitImpulseOffset;
	Vector3 hitImpulseVelocity;
	float hitTiltAngle;
	float hitTiltVelocity;
	float stationaryDuration; // 记录单位静止持续时间，用于平滑移动与站立动画的切换，防止频繁抖动
	readonly List<AnimatedVisualPart> infantryVisualParts = new();
	CpuParticles3D? bowWaveParticles; // 船头浪花（两侧下落水粒）
	CpuParticles3D? sternWakeParticles; // 船尾水波纹（沿水面水平扩散）
	readonly List<Node3D> propellerNodes = new();
	Node3D? helicopterTailRotor;
	bool holdPosition;
	bool parkingAtAirfield;
	bool parkedAtAirfield;
	Vector3 parkedPosition;
	[Export] public RtsUnit? CarrierHome { get; set; }
	public RtsUnit? DockedCarrier { get; private set; }
	public int CarrierSlotIndex { get; private set; } = -1;
	public bool IsDockedOnCarrier => DockedCarrier is not null && GodotObject.IsInstanceValid(DockedCarrier);
	public bool IsCarrierServicing { get; private set; }
	public float CarrierServiceTimeRemaining { get; private set; }
	public bool IsCarrierServiceComplete => IsDockedOnCarrier && !IsCarrierServicing && CarrierServiceTimeRemaining <= 0f;
	public bool IsReturningToCarrier { get; private set; }
	float carrierServiceStartHealth;
	float carrierServiceStartFuel;
	int carrierServiceStartAmmo;
	float carrierServiceFxTimer;

	public int CurrentAmmo { get; set; } = -1;
	public int MaxAmmo { get; private set; } = -1;
	public float AmmoRatio => MaxAmmo <= 0 ? 1f : Mathf.Clamp((float)CurrentAmmo / MaxAmmo, 0f, 1f);
	public bool HasAmmo => MaxAmmo <= 0 || CurrentAmmo > 0;

	bool isCatapultLaunching;
	float catapultProgress;
	Vector3 catapultStartPos;
	Vector3 catapultEndPos;
	Node3D? catapultTargetAfterLaunch;
	readonly List<RtsUnit> carrierAirWing = new();
	float carrierScrambleTimer = 1.0f;
	float carrierReplenishTimer = 0f;
	bool carrierAirWingInitialized = false;
	bool warnedLowFuel;
	bool warnedNoAirfield;
	bool bombingRunActive;
	bool bombingRunOnAttackLeg;
	Vector3 bombingRunStart;
	Vector3 bombingRunApproach;
	Vector3 bombingRunExit;
	Vector3 bombingRunDirection;
	Vector3 bombingRunPerpendicular;
	float bombingRunLength;
	float bombingRunReleaseDistance;
	bool bombingRunVolleyReleased;
	string visualKey = "";
	float bodyFacingYawOffset;
	float visualMotionTime;
	readonly Queue<Vector3> queuedMoveTargets = new();
	readonly List<RuntimeTechBuff> techBuffs = new();
	public bool HasActiveTechBuffs => techBuffs.Count > 0;
	public IEnumerable<(string SourceId, float Remaining)> GetActiveTechBuffInfo()
		=> techBuffs.Select(b => (b.SourceId, b.Remaining));
	readonly List<CpuParticles3D> jetExhaustParticles = new();
	float currentBankRoll;
	float baseVisualRoll;
	Basis visualBaseBasis = Basis.Identity;
	float lastRecordedYaw;
	bool isAirPatrolCircuit;
	int airCombatPhase; // 0: 俯冲接近开火, 1: 跃升改出与高速盘旋
	float airCombatTimer;
	Vector3 airCombatEgressTarget = Vector3.Zero;
	const float ArtilleryDeployDuration = 1.35f;
	const float ArtilleryUndeployDuration = 1.0f;
	float deployTimer = 0f;
	Vector3? pendingMoveAfterUndeploy;
	MeshInstance3D? deployGroundCircle;

	public override void _Ready()
	{
		if (!AllActiveUnits.Contains(this))
			AllActiveUnits.Add(this);
		if (!IsInGroup("rts_units"))
			AddToGroup("rts_units");

		FogRevealed = PlayerOwned;
		FogExplored = PlayerOwned;
		CaptureBaseStats();
		Health = MaxHealth;
		TargetPosition = GlobalPosition;
		InitializeAirUnitState();
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			CollisionLayer = 8u;
			CollisionMask = 0u;
			originalCollisionLayer = 8u;
			originalCollisionMask = 0u;
		}
		else
		{
			originalCollisionLayer = CollisionLayer;
			originalCollisionMask = CollisionMask;
		}
		EnsureCombatOverlays();
		var visRoot = GetNodeOrNull<Node3D>("artillery_model")
			?? GetNodeOrNull<Node3D>("FA18Visual")
			?? GetNodeOrNull<Node3D>("FighterVisual")
			?? GetNodeOrNull<Node3D>("FA18_Fighter")
			?? GetNodeOrNull<Node3D>("SubmarineVisual")
			?? GetNodeOrNull<Node3D>("PatrolBoatVisual")
			?? GetNodeOrNull<Node3D>("PanzerIV")
			?? GetNodeOrNull<Node3D>("Panzer_IV")
			?? GetNodeOrNull<Node3D>("Visual")
			?? GetNodeOrNull<Node3D>("Model");
		if (visRoot is not null)
		{
			ConfigureVisualRig(string.IsNullOrEmpty(visualKey) ? UnitKey : visualKey, visRoot);
		}

		if (BattleUnitCatalog.IsAirUnit(UnitKey) && !parkedAtAirfield)
		{
			StartAirPatrolCircuit();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;
		if (isDying)
		{
			deathTimer += dt;
			if (isAirCrashing)
			{
				// 战机被击落后的物理下坠与失控尾旋动效 (Aerodynamic Flat Spin & Gravity Plunge)
				airCrashVelocity += new Vector3(0f, -22.0f * dt, 0f);
				airCrashVelocity.X = Mathf.MoveToward(airCrashVelocity.X, 0f, dt * 2.5f);
				airCrashVelocity.Z = Mathf.MoveToward(airCrashVelocity.Z, 0f, dt * 2.5f);
				GlobalPosition += airCrashVelocity * dt;

				// 剧烈失控螺旋下坠翻滚 (Corkscrew Roll) 与下俯角
				var visNode = visualRoot ?? this;
				if (GodotObject.IsInstanceValid(visNode))
				{
					visNode.RotateObjectLocal(Vector3.Forward, 7.8f * dt);
					visNode.RotateObjectLocal(Vector3.Right, -2.4f * dt);
				}

				// 触地/触水剧烈殉爆并消散 (Ground/Water Impact Shockwave & Disappearance)
				float crashImpactThreshold = 0.38f;
				if (GlobalPosition.Y <= crashImpactThreshold || deathTimer >= 3.0f)
				{
					BattleFeedback.Destroyed(this, GlobalPosition, true);
					CombatProjectile.SpawnExplosion(this, GlobalPosition, new Color(1.0f, 0.45f, 0.12f), true);

					if (GlobalPosition.Y <= 0.45f)
					{
						BattleFeedback.WaterSpout(this, GlobalPosition);
					}

					DetachAndFadeCrashParticles();
					QueueFree();
					return;
				}
				return;
			}

			if (isHelicopterCrashing)
			{
				// 武装直升机尾桨失效失控旋转坠毁 (Tail Rotor Failure & Uncontrolled Flat Spin)
				airCrashVelocity += new Vector3(0f, -14.0f * dt, 0f);
				airCrashVelocity.X = Mathf.MoveToward(airCrashVelocity.X, 0f, dt * 1.8f);
				airCrashVelocity.Z = Mathf.MoveToward(airCrashVelocity.Z, 0f, dt * 1.8f);
				GlobalPosition += airCrashVelocity * dt;

				RotateY(heliCrashYawVelocity * dt);
				var visNode = visualRoot ?? this;
				if (GodotObject.IsInstanceValid(visNode))
				{
					visNode.RotateObjectLocal(Vector3.Right, -0.8f * dt);
					visNode.RotateObjectLocal(Vector3.Forward, 0.5f * dt);
				}

				if (GlobalPosition.Y <= 0.35f || deathTimer >= 3.2f)
				{
					BattleFeedback.Destroyed(this, GlobalPosition, true);
					CombatProjectile.SpawnExplosion(this, GlobalPosition, new Color(1.0f, 0.45f, 0.12f), true);

					if (GlobalPosition.Y <= 0.45f)
					{
						BattleFeedback.WaterSpout(this, GlobalPosition);
					}

					DetachAndFadeCrashParticles();
					QueueFree();
					return;
				}
				return;
			}

			if (isNavalSinking)
			{
				// 舰船中弹进水沉没 (Progressive Listing, Capsizing & Plunging into Sea)
				GlobalPosition += new Vector3(0f, -navalSinkDepthSpeed * dt, 0f);
				RotateObjectLocal(Vector3.Right, navalSinkPitchRate * dt);
				RotateObjectLocal(Vector3.Forward, navalSinkRollRate * dt);

				// 进水诱发弹药舱次生殉爆 (Secondary Magazine Cook-offs)
				if (deathTimer >= 1.8f && navalSecondaryExplosionsFired == 0)
				{
					navalSecondaryExplosionsFired = 1;
					var cookoffPos = GlobalPosition + GlobalTransform.Basis.Z * 1.5f + Vector3.Up * 0.4f;
					CombatProjectile.SpawnExplosion(this, cookoffPos, new Color(1.0f, 0.5f, 0.12f), true);
					BattleFeedback.WaterSpout(this, cookoffPos);
					BattleFeedback.TriggerCameraShake(cookoffPos, 0.06f, 0.25f);
				}
				else if (deathTimer >= 3.6f && navalSecondaryExplosionsFired == 1)
				{
					navalSecondaryExplosionsFired = 2;
					var cookoffPos2 = GlobalPosition - GlobalTransform.Basis.Z * 1.2f + Vector3.Up * 0.4f;
					CombatProjectile.SpawnExplosion(this, cookoffPos2, new Color(1.0f, 0.38f, 0.08f), true);
					BattleFeedback.WaterSpout(this, cookoffPos2);
					BattleFeedback.TriggerCameraShake(cookoffPos2, 0.07f, 0.28f);
				}

				if (deathTimer >= deathDurationSeconds)
				{
					BattleFeedback.WaterSpout(this, GlobalPosition);
					DetachAndFadeCrashParticles();
					QueueFree();
					return;
				}
				return;
			}

			if (isGroundWreckage)
			{
				// 战车殉爆残骸：飞脱炮塔抛物线坠落，底盘残骸持续燃烧
				if (turretEjected && weaponYawNode is not null && GodotObject.IsInstanceValid(weaponYawNode))
				{
					turretVelocity += new Vector3(0f, -14.0f * dt, 0f);
					weaponYawNode.Position += turretVelocity * dt;
					weaponYawNode.RotateX(turretRotVelocity.X * dt);
					weaponYawNode.RotateY(turretRotVelocity.Y * dt);
					weaponYawNode.RotateZ(turretRotVelocity.Z * dt);

					if (weaponYawNode.Position.Y < 0.12f)
					{
						weaponYawNode.Position = new Vector3(weaponYawNode.Position.X, 0.12f, weaponYawNode.Position.Z);
						turretVelocity = Vector3.Zero;
						turretRotVelocity = Vector3.Zero;
					}
				}

				// 接近残骸寿命终点时向地面缓慢沉降并消散
				if (deathTimer >= deathDurationSeconds - 1.4f)
				{
					GlobalPosition += new Vector3(0f, -0.16f * dt, 0f);
				}

				if (deathTimer >= deathDurationSeconds)
				{
					DetachAndFadeCrashParticles();
					QueueFree();
					return;
				}
				return;
			}

			// 步兵倒地坍缩
			if (BattleUnitCatalog.IsInfantryLike(UnitKey))
			{
				if (visualRoot is not null && GodotObject.IsInstanceValid(visualRoot))
				{
					var r = visualRoot.Rotation;
					visualRoot.Rotation = new Vector3(Mathf.Lerp(r.X, -Mathf.Pi * 0.46f, dt * 7.5f), r.Y, r.Z);
				}
				if (deathTimer >= deathDurationSeconds - 0.8f)
				{
					GlobalPosition += new Vector3(0f, -0.08f * dt, 0f);
				}
				if (deathTimer >= deathDurationSeconds)
				{
					QueueFree();
					return;
				}
				return;
			}

			// 通用保底销毁
			if (deathTimer >= deathDurationSeconds)
			{
				DetachAndFadeCrashParticles();
				QueueFree();
			}
			return;
		}

		if (IsDockedOnCarrier)
		{
			ProcessDockedOnCarrierPhysics(dt);
			return;
		}

		if (UnitKey == "aircraft_carrier")
		{
			ProcessCarrierLogic(dt);
		}

		visualMotionTime += dt;
		if (cooldownLeft > 0f)
			cooldownLeft -= dt;
		if (grenadeCooldownLeft > 0f)
			grenadeCooldownLeft -= dt;

		// 武器开火后坐力机械弹簧阻尼复位 (Recoil Spring-Damper Dynamics)
		const float RecoilSpringK = 48.0f;
		const float RecoilDampingC = 12.0f;
		const float RecoilMassM = 1.0f;
		float recoilAccel = (-RecoilSpringK * weaponRecoilOffset - RecoilDampingC * weaponRecoilVelocity) / RecoilMassM;
		weaponRecoilVelocity += recoilAccel * dt;
		weaponRecoilOffset += weaponRecoilVelocity * dt;
		if (Mathf.Abs(weaponRecoilOffset) < 0.0004f && Mathf.Abs(weaponRecoilVelocity) < 0.001f)
		{
			weaponRecoilOffset = 0f;
			weaponRecoilVelocity = 0f;
		}
		if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode) && hasRecordedWeaponPitchRest)
		{
			if (UnitKey == "artillery")
				weaponPitchNode.Position = weaponPitchRestPosition;
			else
				weaponPitchNode.Position = weaponPitchRestPosition + new Vector3(0f, 0f, weaponRecoilOffset);
		}

		if (chassisRecoilPitch > 0.001f || chassisRecoilPitchVel != 0f)
		{
			chassisRecoilPitchVel += (-36f * chassisRecoilPitch - 10f * chassisRecoilPitchVel) * dt;
			chassisRecoilPitch += chassisRecoilPitchVel * dt;
			if (Mathf.Abs(chassisRecoilPitch) < 0.001f)
			{
				chassisRecoilPitch = 0f;
				chassisRecoilPitchVel = 0f;
			}
		}

		UpdateArtilleryDeployment(dt);

		if (DeployState is ArtilleryDeployState.Deploying or ArtilleryDeployState.Undeploying)
		{
			Velocity = Vector3.Zero;
			MoveAndSlide();
			MaintainMovementLayer();
			UpdateVisualState(dt);
			return;
		}

		UpdateTechBuffs(dt);

		if (UpdateAirUnitFuelState(dt))
		{
			MaintainMovementLayer();
			UpdateVisualState(dt);
			return;
		}

		if (parkedAtAirfield)
		{
			Velocity = Vector3.Zero;
			MaintainMovementLayer();
			UpdateVisualState(dt);
			return;
		}

		if (bombingRunActive)
		{
			ProcessBombingRun(dt);
			UpdateVisualState(dt);
			return;
		}

		if (GodotObject.IsInstanceValid(AttackTarget))
			ProcessAttack(dt);
		else
		{
			if (AttackGrounding)
				ProcessAttackGround(dt);
			else if (Patrolling)
				ProcessPatrol(dt);
			else if (Guarding)
				ProcessGuard(dt);
			else if (AttackMoving)
				AcquireAttackMoveTarget(dt);
			else if (!holdPosition)
			{
				if (IsIdle())
					AcquireGuardTarget(dt);
				else if (CanFireWhileMoving)
					AcquireOpportunisticTarget(dt);
			}
			ProcessMove(dt);
		}

		ResolveLocalSeparation(dt);
		UpdateVisualState(dt);
	}

	public override void _ExitTree()
	{
		AllActiveUnits.Remove(this);
		if (airCrashSmoke is not null && GodotObject.IsInstanceValid(airCrashSmoke) && airCrashSmoke.GetParent() == this)
		{
			airCrashSmoke.QueueFree();
			airCrashSmoke = null;
		}
		if (airCrashFire is not null && GodotObject.IsInstanceValid(airCrashFire) && airCrashFire.GetParent() == this)
		{
			airCrashFire.QueueFree();
			airCrashFire = null;
		}
		if (navalSinkSmoke is not null && GodotObject.IsInstanceValid(navalSinkSmoke) && navalSinkSmoke.GetParent() == this)
		{
			navalSinkSmoke.QueueFree();
			navalSinkSmoke = null;
		}
		if (navalSinkBubbles is not null && GodotObject.IsInstanceValid(navalSinkBubbles) && navalSinkBubbles.GetParent() == this)
		{
			navalSinkBubbles.QueueFree();
			navalSinkBubbles = null;
		}
		if (wreckageSmoke is not null && GodotObject.IsInstanceValid(wreckageSmoke) && wreckageSmoke.GetParent() == this)
		{
			wreckageSmoke.QueueFree();
			wreckageSmoke = null;
		}
		if (wreckageFire is not null && GodotObject.IsInstanceValid(wreckageFire) && wreckageFire.GetParent() == this)
		{
			wreckageFire.QueueFree();
			wreckageFire = null;
		}
		if (groundScorchDecal is not null && GodotObject.IsInstanceValid(groundScorchDecal) && groundScorchDecal.GetParent() == this)
		{
			groundScorchDecal.QueueFree();
			groundScorchDecal = null;
		}
	}

	public void MoveTo(Vector3 worldPos)
	{
		if (IsDockedOnCarrier)
		{
			if (IsCarrierServicing)
			{
				if (PlayerOwned)
				{
					FindHud()?.ShowAlert($"{DisplayName} 正在航母甲板进行 10 秒补给整备中 (剩余 {CarrierServiceTimeRemaining:F1}s)，无法起飞");
				}
				return;
			}
			LaunchFromCarrier(null);
			TargetPosition = NormalizeCommandPosition(worldPos);
			return;
		}

		ClearSpecialOrders();
		ClearQueuedMovement();
		isManualMoveOrder = true;
		if (!CanFireWhileMoving || AttackTarget is null || !IsLiveEnemyTarget(AttackTarget) || GlobalPosition.DistanceTo(AttackTarget.GlobalPosition) > AttackRange)
		{
			AttackTarget = null;
		}
		AttackMoving = false;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = false;

		if (SupportsArtilleryDeploy && (DeployState == ArtilleryDeployState.Deployed || DeployState == ArtilleryDeployState.Deploying))
		{
			pendingMoveAfterUndeploy = NormalizeCommandPosition(worldPos);
			TargetPosition = GlobalPosition;
			BeginArtilleryUndeploy();
			return;
		}

		pendingMoveAfterUndeploy = null;
		TargetPosition = NormalizeCommandPosition(worldPos);
		acquireTimer = 0f;
	}

	public void AttackMoveTo(Vector3 worldPos)
	{
		if (IsDockedOnCarrier)
		{
			if (IsCarrierServicing)
			{
				if (PlayerOwned)
				{
					FindHud()?.ShowAlert($"{DisplayName} 正在航母甲板进行 10 秒补给整备中 (剩余 {CarrierServiceTimeRemaining:F1}s)，无法起飞");
				}
				return;
			}
			LaunchFromCarrier(null);
			TargetPosition = NormalizeCommandPosition(worldPos);
			return;
		}

		ClearSpecialOrders();
		ClearQueuedMovement();
		isManualMoveOrder = false;
		AttackTarget = null;
		AttackMoving = true;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = false;

		if (SupportsArtilleryDeploy && (DeployState == ArtilleryDeployState.Deployed || DeployState == ArtilleryDeployState.Deploying))
		{
			pendingMoveAfterUndeploy = NormalizeCommandPosition(worldPos);
			TargetPosition = GlobalPosition;
			BeginArtilleryUndeploy();
			return;
		}

		pendingMoveAfterUndeploy = null;
		TargetPosition = NormalizeCommandPosition(worldPos);
		acquireTimer = 0f;
	}

	public bool Attack(Node3D node, float slotAngleOffset = 0f)
	{
		if (!CanAttackTarget(node))
			return false;

		if (IsDockedOnCarrier)
		{
			if (IsCarrierServicing)
			{
				if (PlayerOwned)
				{
					FindHud()?.ShowAlert($"{DisplayName} 正在航母甲板进行 10 秒补给整备中 (剩余 {CarrierServiceTimeRemaining:F1}s)，无法起飞");
				}
				return false;
			}
			LaunchFromCarrier(node);
			return true;
		}

		if (UnitKey == "bomber")
		{
			StartBombingRunOnPosition(node.GlobalPosition);
			return true;
		}

		ClearSpecialOrders();
		ClearQueuedMovement();
		isManualMoveOrder = false;
		AttackTarget = node;
		attackSlotAngleOffset = slotAngleOffset;
		AttackMoving = false;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = false;
		return true;
	}

	public bool AttackGround(Vector3 worldPos)
	{
		if (UnitKey == "bomber")
		{
			StartBombingRunOnPosition(worldPos);
			return true;
		}

		if (!CanAttackGroundPoint)
			return false;

		ClearSpecialOrders();
		ClearQueuedMovement();
		AttackTarget = null;
		AttackMoving = false;
		AttackGrounding = true;
		Patrolling = false;
		Guarding = false;
		guardTarget = null;
		attackGroundTarget = NormalizeAttackGroundTarget(worldPos);
		TargetPosition = NormalizeCommandPosition(attackGroundTarget);
		return true;
	}

	public void PatrolTo(Vector3 worldPos)
		=> PatrolBetween(GlobalPosition, worldPos);

	public void PatrolBetween(Vector3 pointA, Vector3 pointB)
	{
		ClearSpecialOrders();
		ClearQueuedMovement();
		AttackTarget = null;
		AttackMoving = false;
		AttackGrounding = false;
		Guarding = false;
		Patrolling = true;
		guardTarget = null;
		patrolA = NormalizeCommandPosition(pointA);
		patrolB = NormalizeCommandPosition(pointB);
		patrolHeadingToB = true;
		TargetPosition = patrolB;
		acquireTimer = 0f;
	}

	public void Guard(RtsUnit ally)
	{
		if (!GodotObject.IsInstanceValid(ally) || ally == this || ally.PlayerOwned != PlayerOwned)
			return;

		ClearSpecialOrders();
		ClearQueuedMovement();
		AttackTarget = null;
		AttackMoving = false;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = true;
		guardTarget = ally;
		acquireTimer = 0f;
		TargetPosition = NormalizeCommandPosition(GuardFollowPosition());
	}

	public void Stop()
	{
		CancelBombingRun();
		if (!parkedAtAirfield)
			parkingAtAirfield = false;
		ClearQueuedMovement();
		isManualMoveOrder = false;
		AttackTarget = null;
		attackSlotAngleOffset = 0f;
		AttackMoving = false;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = false;
		guardTarget = null;
		pendingMoveAfterUndeploy = null;
		TargetPosition = parkedAtAirfield
			? new Vector3(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z)
			: GlobalPosition;
		Velocity = Vector3.Zero;
	}

	public void HoldPosition()
	{
		Stop();
		holdPosition = true;
	}

	public bool IsHoldingPosition => holdPosition;

	public void ToggleArtilleryDeploy()
	{
		if (!SupportsArtilleryDeploy || IsDead)
			return;

		if (DeployState == ArtilleryDeployState.Mobile || DeployState == ArtilleryDeployState.Undeploying)
		{
			BeginArtilleryDeploy();
		}
		else
		{
			BeginArtilleryUndeploy();
		}
	}

	public void BeginArtilleryDeploy()
	{
		if (!SupportsArtilleryDeploy || IsDead)
			return;

		if (DeployState == ArtilleryDeployState.Deployed || DeployState == ArtilleryDeployState.Deploying)
			return;

		if (DeployState == ArtilleryDeployState.Undeploying)
		{
			deployTimer = Mathf.Clamp(ArtilleryDeployDuration * (1f - (deployTimer / ArtilleryUndeployDuration)), 0f, ArtilleryDeployDuration);
		}
		else
		{
			deployTimer = 0f;
		}

		DeployState = ArtilleryDeployState.Deploying;
		Velocity = Vector3.Zero;
		EnsureDeployGroundCircle();
	}

	public void BeginArtilleryUndeploy()
	{
		if (!SupportsArtilleryDeploy || IsDead)
			return;

		if (DeployState == ArtilleryDeployState.Mobile || DeployState == ArtilleryDeployState.Undeploying)
			return;

		if (DeployState == ArtilleryDeployState.Deploying)
		{
			deployTimer = Mathf.Clamp(ArtilleryUndeployDuration * (1f - (deployTimer / ArtilleryDeployDuration)), 0f, ArtilleryUndeployDuration);
		}
		else
		{
			deployTimer = 0f;
		}

		DeployState = ArtilleryDeployState.Undeploying;
		Velocity = Vector3.Zero;
	}

	void EnsureDeployGroundCircle()
	{
		if (deployGroundCircle is not null && GodotObject.IsInstanceValid(deployGroundCircle))
			return;

		var circleMesh = new TorusMesh
		{
			InnerRadius = 1.65f,
			OuterRadius = 1.82f,
			Rings = 24,
			RingSegments = 16
		};
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = PlayerOwned ? new Color(0.24f, 0.85f, 1.0f, 0.85f) : new Color(1.0f, 0.35f, 0.20f, 0.85f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		deployGroundCircle = new MeshInstance3D
		{
			Name = "DeployGroundAnchorCircle",
			Mesh = circleMesh,
			MaterialOverride = mat,
			Position = new Vector3(0f, 0.04f, 0f),
			Visible = false
		};
		AddChild(deployGroundCircle);
	}

	void UpdateArtilleryDeployment(float dt)
	{
		if (!SupportsArtilleryDeploy)
			return;

		if (DeployState == ArtilleryDeployState.Deploying)
		{
			deployTimer += dt;
			DeployProgress = Mathf.Clamp(deployTimer / ArtilleryDeployDuration, 0f, 1f);
			Velocity = Vector3.Zero;

			if (UnitKey == "artillery" && weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				float targetPitch = Mathf.Lerp(0f, Mathf.DegToRad(58f), DeployProgress);
				weaponPitchNode.Rotation = new Vector3(targetPitch, weaponPitchNode.Rotation.Y, weaponPitchNode.Rotation.Z);
			}
			else if (BattleUnitCatalog.IsInfantryLike(UnitKey))
			{
				crouchRatio = Mathf.Lerp(0f, 0.85f, DeployProgress);
			}

			if (deployGroundCircle is not null && GodotObject.IsInstanceValid(deployGroundCircle))
			{
				deployGroundCircle.Visible = true;
				float scale = Mathf.Lerp(0.35f, 1.0f, DeployProgress);
				deployGroundCircle.Scale = new Vector3(scale, 1f, scale);
			}

			if (deployTimer >= ArtilleryDeployDuration)
			{
				DeployState = ArtilleryDeployState.Deployed;
				DeployProgress = 1f;
			}
		}
		else if (DeployState == ArtilleryDeployState.Undeploying)
		{
			deployTimer += dt;
			DeployProgress = Mathf.Clamp(1f - (deployTimer / ArtilleryUndeployDuration), 0f, 1f);
			Velocity = Vector3.Zero;

			if (UnitKey == "artillery" && weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				float targetPitch = Mathf.Lerp(0f, Mathf.DegToRad(58f), DeployProgress);
				weaponPitchNode.Rotation = new Vector3(targetPitch, weaponPitchNode.Rotation.Y, weaponPitchNode.Rotation.Z);
			}
			else if (BattleUnitCatalog.IsInfantryLike(UnitKey))
			{
				crouchRatio = Mathf.Lerp(0f, 0.85f, DeployProgress);
			}

			if (deployGroundCircle is not null && GodotObject.IsInstanceValid(deployGroundCircle))
			{
				deployGroundCircle.Visible = true;
				float scale = Mathf.Lerp(0.35f, 1.0f, DeployProgress);
				deployGroundCircle.Scale = new Vector3(scale, 1f, scale);
			}

			if (deployTimer >= ArtilleryUndeployDuration)
			{
				DeployState = ArtilleryDeployState.Mobile;
				DeployProgress = 0f;
				if (deployGroundCircle is not null && GodotObject.IsInstanceValid(deployGroundCircle))
				{
					deployGroundCircle.Visible = false;
				}
				if (pendingMoveAfterUndeploy is { } pending)
				{
					TargetPosition = pending;
					pendingMoveAfterUndeploy = null;
				}
			}
		}
		else if (DeployState == ArtilleryDeployState.Deployed)
		{
			DeployProgress = 1f;
			if (deployGroundCircle is not null && GodotObject.IsInstanceValid(deployGroundCircle))
			{
				deployGroundCircle.Visible = true;
				deployGroundCircle.Scale = Vector3.One;
			}
			if (BattleUnitCatalog.IsInfantryLike(UnitKey))
			{
				crouchRatio = 0.85f;
			}
		}
		else // Mobile
		{
			DeployProgress = 0f;
			if (deployGroundCircle is not null && GodotObject.IsInstanceValid(deployGroundCircle))
			{
				deployGroundCircle.Visible = false;
			}
		}
	}

	public void MoveAlongPath(IEnumerable<Vector3> worldPath, bool holdAtEnd = false)
	{
		ClearSpecialOrders();
		ClearQueuedMovement();
		var path = worldPath
			.Select(NormalizeCommandPosition)
			.Where(point => point.DistanceSquaredTo(GlobalPosition) > 0.04f)
			.ToArray();
		if (path.Length == 0)
		{
			holdPosition = holdAtEnd;
			return;
		}

		TargetPosition = path[0];
		for (var i = 1; i < path.Length; i++)
			queuedMoveTargets.Enqueue(path[i]);
		holdPosition = holdAtEnd;
	}

	public void BeginAirfieldParking(IEnumerable<Vector3> worldPath, Vector3 parkingTarget)
	{
		if (!SupportsAirfieldParking)
			return;

		CancelBombingRun();
		ClearQueuedMovement();
		parkingAtAirfield = true;
		parkedAtAirfield = false;
		parkedPosition = NormalizeCommandPosition(parkingTarget);
		AttackTarget = null;
		AttackMoving = false;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = false;
		guardTarget = null;

		var path = worldPath
			.Select(NormalizeCommandPosition)
			.Where(point => point.DistanceSquaredTo(GlobalPosition) > 0.04f)
			.ToArray();
		if (path.Length == 0)
		{
			TargetPosition = parkedPosition;
			ParkAtAirfield();
			return;
		}

		TargetPosition = path[0];
		for (var i = 1; i < path.Length; i++)
			queuedMoveTargets.Enqueue(path[i]);
		holdPosition = true;
	}

	public void StartBombingRunOnPosition(Vector3 targetWorldPos)
	{
		var flatTarget = new Vector3(targetWorldPos.X, 0f, targetWorldPos.Z);
		var flatSelf = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
		var flatDelta = flatTarget - flatSelf;
		Vector3 runDir;
		if (flatDelta.LengthSquared() > 1.0f)
		{
			runDir = flatDelta.Normalized();
		}
		else
		{
			var fwd = -GlobalTransform.Basis.Z;
			fwd.Y = 0f;
			runDir = fwd.LengthSquared() > 0.01f ? fwd.Normalized() : Vector3.Forward;
		}

		// 规划一条以目标为中心穿行、长 22m 的固定直线地毯式轰炸航线
		float halfLength = 11f;
		var runStart = flatTarget - runDir * (halfLength * 0.8f);
		var runEnd = flatTarget + runDir * (halfLength * 1.2f);
		ApplyBombingRunCommand(runStart, runEnd);
	}

	public void ApplyBombingRunCommand(Vector3 start, Vector3 end)
	{
		if (!SupportsBombingRun)
			return;

		ClearQueuedMovement();
		parkingAtAirfield = false;
		parkedAtAirfield = false;
		AttackTarget = null;
		AttackMoving = false;
		AttackGrounding = false;
		Patrolling = false;
		Guarding = false;
		guardTarget = null;
		holdPosition = false;

		var flatStart = new Vector3(start.X, 0f, start.Z);
		var flatEnd = new Vector3(end.X, 0f, end.Z);
		var flatDelta = flatEnd - flatStart;
		if (flatDelta.LengthSquared() < 0.01f)
		{
			var fallback = -Basis.Z;
			fallback.Y = 0f;
			if (fallback.LengthSquared() < 0.01f)
				fallback = Vector3.Forward;
			flatDelta = fallback.Normalized() * BomberMinBombingRunLength;
		}

		bombingRunDirection = flatDelta.Normalized();
		bombingRunLength = Mathf.Clamp(flatDelta.Length(), BomberMinBombingRunLength, BomberMaxBombingRunLength);
		bombingRunPerpendicular = Vector3.Up.Cross(bombingRunDirection).Normalized();

		var height = CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
		bombingRunStart = new Vector3(flatStart.X, height, flatStart.Z);
		bombingRunApproach = bombingRunStart - bombingRunDirection * BomberApproachLead;
		var bombingRunEnd = bombingRunStart + bombingRunDirection * bombingRunLength;
		bombingRunExit = bombingRunEnd + bombingRunDirection * BomberExitLead;
		bombingRunReleaseDistance = Mathf.Clamp(
			bombingRunLength * BomberReleaseProgress,
			BomberReleaseSpacingForward,
			Mathf.Max(BomberReleaseSpacingForward, bombingRunLength - BomberReleaseSpacingForward));
		bombingRunVolleyReleased = false;
		bombingRunOnAttackLeg = false;
		bombingRunActive = true;
		TargetPosition = NormalizeCommandPosition(bombingRunApproach);
	}

	public void ApplyCatalogCombatProfile()
	{
		if (SplashRadius <= 0.05f)
			SplashRadius = BattleUnitCatalog.SplashRadius(UnitKey);
		if (SplashFalloff <= 0f || SplashFalloff > 1f)
			SplashFalloff = BattleUnitCatalog.SplashFalloff(UnitKey);
		CaptureBaseStats();
	}

	public void ConfigureFromDefinition(BattleUnitDefinition def, bool playerOwned, string displayName)
	{
		var preserveHealthRatio = Health > 0f && MaxHealth > 0.01f;
		var healthRatio = preserveHealthRatio
			? Mathf.Clamp(Health / MaxHealth, 0f, 1f)
			: 1f;

		UnitKey = def.Key;
		DisplayName = string.IsNullOrWhiteSpace(displayName) ? def.DisplayName : displayName.Trim();
		PlayerOwned = playerOwned;
		MaxHealth = def.MaxHealth;
		MoveSpeed = def.MoveSpeed;
		AttackDamage = def.AttackDamage;
		AttackRange = def.AttackRange;
		AttackCooldown = def.AttackCooldown;
		SplashRadius = BattleUnitCatalog.SplashRadius(def.Key);
		SplashFalloff = BattleUnitCatalog.SplashFalloff(def.Key);
		GoldCost = def.GoldCost;
		PopCost = def.PopCost;
		CruiseHeight = BattleUnitCatalog.SpawnHeight(def.Key);

		ResetCapturedBaseStats();
		if (techBuffs.Count > 0)
			RecalculateTechBuffStats();
		else
			CaptureBaseStats();

		Health = preserveHealthRatio
			? Mathf.Clamp(MaxHealth * healthRatio, 1f, MaxHealth)
			: MaxHealth;

		if (def.Key is "heavy_tank" or "anti_air_gun" or "fighter" or "bomber")
		{
			MaxShield = def.MaxHealth * 0.4f;
			Shield = MaxShield;
		}
		else
		{
			MaxShield = 0f;
			Shield = 0f;
		}
		secondsSinceLastDamage = 99f;

		InitializeAirUnitState();
		RefreshCombatOverlays();
	}

	void InitializeAirUnitState()
	{
		if (!BattleUnitCatalog.IsAirUnit(UnitKey))
			return;

		maxFuelSeconds = UnitKey switch
		{
			"bomber" => 38f,
			"fighter" => 28f,
			"scout_plane" => 32f,
			"scout_helicopter" => 30f,
			_ => DefaultBattleFuelSeconds
		};
		lowFuelReturnRatio = DefaultLowFuelReturnRatio;
		refuelSeconds = UnitKey switch
		{
			"bomber" => 4.5f,
			"fighter" => 3.5f,
			"scout_plane" => 3.0f,
			_ => 3.5f
		};
		MaxAmmo = BattleUnitCatalog.GetAirUnitMaxAmmo(UnitKey);
		CurrentAmmo = MaxAmmo;

		if (CurrentFuel <= 0f || CurrentFuel > maxFuelSeconds)
			CurrentFuel = maxFuelSeconds;
	}

	bool UpdateAirUnitFuelState(float delta)
	{
		if (!BattleUnitCatalog.IsAirUnit(UnitKey))
			return false;

		if (IsDockedOnCarrier)
		{
			// 停靠航母期间由甲板整备系统负责注油与抢修，不消耗飞行燃油
			return true;
		}

		if (CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead)
		{
			// 航母舰载机在空巡航或战斗：消耗燃油
			CurrentFuel = Mathf.Max(0f, CurrentFuel - delta);
			if (CurrentFuel <= 0f)
			{
				CurrentFuel = 0f;
				if (!IsReturningToCarrier)
				{
					ReturnToCarrierBase();
				}
				return false;
			}

			if (!IsReturningToCarrier && ShouldReturnForCarrier())
			{
				ReturnToCarrierBase();
				return false;
			}
			return false;
		}

		if (!SupportsAirfieldParking || BattleGameManager.DisableFuelDepletion)
		{
			CurrentFuel = maxFuelSeconds;
			warnedLowFuel = false;
			warnedNoAirfield = false;
			return false;
		}

		if (parkedAtAirfield)
		{
			RefuelWhileParked(delta);
			return true;
		}

		// 飞行中持续消耗燃油
		CurrentFuel = Mathf.Max(0f, CurrentFuel - delta);

		// 没油了绝不在空中直接自爆，启动应急滑翔动力飞回基地加油！
		if (CurrentFuel <= 0f)
		{
			CurrentFuel = 0f;
			if (!parkingAtAirfield)
			{
				var manager = BattleGameManager.Instance;
				if (manager is not null && manager.TryBeginAircraftRefuelReturn(this, true, out var returnMsg))
				{
					if (PlayerOwned && !warnedLowFuel)
					{
						FindHud()?.ShowAlert($"{DisplayName} 燃油耗尽，正在紧急滑翔返场加油！");
						warnedLowFuel = true;
					}
					warnedNoAirfield = false;
				}
			}
			return false;
		}

		// 燃油警戒或弹药打光：自动触发返航加油整备
		if (!parkingAtAirfield && ShouldReturnForFuel())
		{
			var manager = BattleGameManager.Instance;
			var message = "";
			if (manager is not null && manager.TryBeginAircraftRefuelReturn(this, true, out message))
			{
				if (PlayerOwned && !warnedLowFuel)
				{
					FindHud()?.ShowAlert(message);
					warnedLowFuel = true;
				}
				warnedNoAirfield = false;
				return false;
			}

			if (PlayerOwned && !warnedNoAirfield && !string.IsNullOrWhiteSpace(message))
			{
				FindHud()?.ShowAlert(message);
				warnedNoAirfield = true;
			}
		}

		return false;
	}

	public bool ShouldReturnForCarrier()
	{
		if (CarrierHome is null || !GodotObject.IsInstanceValid(CarrierHome) || CarrierHome.IsDead)
			return false;

		// 1. 弹药耗尽 (Ammunition Depleted)
		if (MaxAmmo > 0 && CurrentAmmo <= 0)
			return true;

		// 2. 机体损伤严重 (Health Damaged <= 50%)
		if (Health < MaxHealth * 0.50f)
			return true;

		// 3. 燃油告急 (Fuel Low <= 25% 或不够飞回母舰)
		if (FuelRatio <= 0.25f)
			return true;

		var flatSelf = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
		var flatCarrier = new Vector3(CarrierHome.GlobalPosition.X, 0f, CarrierHome.GlobalPosition.Z);
		var returnSpeed = Mathf.Max(0.1f, MoveSpeed * 1.08f);
		var fuelNeeded = flatSelf.DistanceTo(flatCarrier) / returnSpeed + ReturnFuelReserveSeconds;
		return CurrentFuel <= fuelNeeded;
	}

	bool ShouldReturnForFuel()
	{
		// 1. 弹药耗尽（如战略轰炸机投弹完毕）立即返航装填
		if (MaxAmmo > 0 && CurrentAmmo <= 0)
			return true;

		// 2. 燃油低于 25% 警戒线自动返航
		if (FuelRatio <= Mathf.Max(0.25f, lowFuelReturnRatio))
			return true;

		if (BattleGameManager.Instance?.ResolveNearestAirfield(PlayerOwned, GlobalPosition) is not { } airfield)
			return false;

		var flatSelf = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
		var flatTarget = new Vector3(airfield.GlobalPosition.X, 0f, airfield.GlobalPosition.Z);
		var returnSpeed = Mathf.Max(0.1f, MoveSpeed * 1.08f);
		var fuelNeededToReturn = flatSelf.DistanceTo(flatTarget) / returnSpeed + ReturnFuelReserveSeconds;
		return CurrentFuel <= fuelNeededToReturn;
	}

	void RefuelWhileParked(float delta)
	{
		// 快速加满燃油 (3~4秒注满)
		var rate = maxFuelSeconds / Mathf.Max(2f, refuelSeconds);
		CurrentFuel = Mathf.Min(maxFuelSeconds, CurrentFuel + rate * delta);

		// 补满机载弹药
		if (MaxAmmo > 0)
		{
			CurrentAmmo = MaxAmmo;
		}

		// 地勤抢修机体损伤
		if (Health < MaxHealth)
		{
			Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.25f * delta);
		}

		// 地勤维修检修火花与烟气
		carrierServiceFxTimer += delta;
		if (carrierServiceFxTimer >= 0.7f)
		{
			carrierServiceFxTimer = 0f;
			EffectPool.Instance?.SpawnRepairSparks(GlobalPosition + Vector3.Up * 0.35f);
		}

		// 燃油加满且补给完毕：自动升空出击，重回战场！
		if (CurrentFuel >= maxFuelSeconds * 0.99f)
		{
			warnedLowFuel = false;
			warnedNoAirfield = false;
			carrierServiceFxTimer = 0f;
			TakeoffAndResumeCombat();
		}
	}

	public void TakeoffAndResumeCombat()
	{
		if (!parkedAtAirfield)
			return;

		parkedAtAirfield = false;
		parkingAtAirfield = false;
		holdPosition = false;

		var cruiseH = CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
		GlobalPosition = new Vector3(GlobalPosition.X, cruiseH, GlobalPosition.Z);

		var forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward.LengthSquared() < 0.01f) forward = Vector3.Forward;
		forward = forward.Normalized();

		Velocity = forward * (MoveSpeed * 1.25f);
		TargetPosition = GlobalPosition + forward * 32f;

		// 自动搜寻最近的敌军单位或建筑重新出击
		var manager = BattleGameManager.Instance;
		Node3D? enemyTarget = null;
		if (manager is not null)
		{
			enemyTarget = manager.GetUnits(!PlayerOwned)
				.Where(u => GodotObject.IsInstanceValid(u) && !u.IsDead)
				.OrderBy(u => u.GlobalPosition.DistanceSquaredTo(GlobalPosition))
				.FirstOrDefault();
			if (enemyTarget is null)
			{
				enemyTarget = manager.GetBuildings(!PlayerOwned)
					.Where(b => GodotObject.IsInstanceValid(b) && b.Health > 0f)
					.OrderBy(b => b.GlobalPosition.DistanceSquaredTo(GlobalPosition))
					.FirstOrDefault();
			}
		}

		if (enemyTarget is not null)
		{
			Attack(enemyTarget);
		}
		else
		{
			if (BattleUnitCatalog.IsFixedWingAircraft(UnitKey))
			{
				StartAirLoiterOrbit(TargetPosition);
			}
		}

		if (PlayerOwned)
		{
			FindHud()?.ShowAlert($"{DisplayName} 燃油弹药整备完毕，已重新升空出击！");
		}
	}

	void CrashFromFuelLoss()
	{
		if (IsDead)
			return;

		CurrentFuel = 0f;
		if (PlayerOwned)
			FindHud()?.ShowAlert($"{DisplayName} 燃油耗尽，正在紧急就近迫降返场");
	}

	BattleHud? FindHud()
		=> IsInsideTree() ? GetTree()?.CurrentScene?.GetNodeOrNull<BattleHud>("HUD") : null;

	public void ApplyDamage(float amount, Node3D? attacker = null, bool isCrit = false, bool isRearHit = false, bool isRicochet = false)
	{
		if (IsDead)
			return;

		float beforeShield = Shield;

		if (amount > 0f)
		{
			secondsSinceLastDamage = 0f;
			if (attacker is not null && IsLiveEnemyTarget(attacker))
			{
				OnDamagedBy(attacker);
			}

			// 1. 联盟阵地战减伤特技：如果是联盟核心兵种且在己方防御塔或基地 16 米内，伤害减少 15%
			if (UnitKey is "tank" or "medium_tank" or "artillery" or "infantry")
			{
				if (BattleGameManager.Instance is { } manager)
				{
					bool nearDefense = false;
					foreach (var building in manager.GetBuildings(PlayerOwned))
					{
						if (GodotObject.IsInstanceValid(building) && building.Health > 0f && (building.IsDefenseTurret || building.IsMainBase))
						{
							if (GlobalPosition.DistanceTo(building.GlobalPosition) <= 16f)
							{
								nearDefense = true;
								break;
							}
						}
					}
					if (nearDefense)
					{
						amount *= 0.85f;
					}
				}
			}

			// 2. 科技伤害减免（科技树升级加成）
			if (techDamageReduction > 0f)
				amount *= Mathf.Clamp(1f - techDamageReduction, 0.25f, 1f);

			// 3. 智能军等离子护盾吸收
			if (MaxShield > 0f && Shield > 0f)
			{
				if (Shield >= amount)
				{
					Shield -= amount;
					amount = 0f;
				}
				else
				{
					amount -= Shield;
					Shield = 0f;
				}
			}
		}

		var before = Health;
		Health = Mathf.Max(0f, Health - amount);

		var shieldDealt = beforeShield - Shield;
		var totalDealt = (before - Health) + shieldDealt;

		BattleFeedback.Damage(this, GlobalPosition, totalDealt, PlayerOwned, Health <= 0f, isCrit, isRearHit, isRicochet);

		// 模型受击物理与视觉反馈：瞬间受击白闪、微冲量顿挫与跳弹火花
		if (amount > 0.5f)
		{
			hitFlashTimer = 0.09f;
			Vector3 impactDir = attacker is not null && GodotObject.IsInstanceValid(attacker)
				? (GlobalPosition - attacker.GlobalPosition).Normalized()
				: -GlobalTransform.Basis.Z;
			impactDir.Y = 0f;
			if (impactDir.LengthSquared() < 0.01f) impactDir = -GlobalTransform.Basis.Z;

			float power = Mathf.Clamp(amount / Mathf.Max(10f, MaxHealth * 0.15f), 0.2f, 1.2f);
			if (isCrit || isRearHit) power *= 1.45f;

			float maxImpulse = BattleUnitCatalog.IsInfantryLike(UnitKey) ? 0.14f : 0.05f;
			hitImpulseVelocity += impactDir * (maxImpulse * power * 18.0f);
			hitTiltVelocity += power * 0.10f;

			// 生成装甲金属跳弹火星与受击火花
			Vector3 hitPos = GlobalPosition + Vector3.Up * (BattleUnitCatalog.IsInfantryLike(UnitKey) ? 0.85f : 0.65f);
			Color sparkColor = isRicochet ? new Color(0.85f, 0.95f, 1.0f) : isCrit ? new Color(1.0f, 0.9f, 0.3f) : new Color(1.0f, 0.75f, 0.25f);
			CombatProjectile.SpawnBulletImpact(this, hitPos, sparkColor, isRicochet ? -impactDir.Rotated(Vector3.Up, 0.6f) : -impactDir);

			// 步兵写实受击微顿挫后仰动画
			if (BattleUnitCatalog.IsInfantryLike(UnitKey) && !string.IsNullOrEmpty(hitAnimationName) && visualAnimationLockRemaining <= 0f && visualAnimationPlayers.Count > 0)
			{
				foreach (var player in visualAnimationPlayers)
				{
					if (player.HasAnimation(hitAnimationName))
						player.Play(hitAnimationName, customBlend: 0.08f);
				}
				visualAnimationLockRemaining = 0.22f;
			}
		}

		if (Health <= 0f && !isDying)
		{
			isDying = true;
			deathTimer = 0f;
			AllActiveUnits.Remove(this);
			GameState.Instance?.DeselectNode(this);
			CollisionLayer = 0;
			CollisionMask = 0;

			if (selectionRing is not null && GodotObject.IsInstanceValid(selectionRing))
				selectionRing.Visible = false;
			if (techRing is not null && GodotObject.IsInstanceValid(techRing))
				techRing.Visible = false;
			if (GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar") is { } hpBar)
				hpBar.Visible = false;

			EmitSignal(SignalName.Died, this);

			if (UnitKey == "aircraft_carrier")
			{
				for (int i = 0; i < carrierAirWing.Count; i++)
				{
					var p = carrierAirWing[i];
					if (GodotObject.IsInstanceValid(p))
					{
						if (p.IsDockedOnCarrier)
							p.ApplyDamage(p.MaxHealth * 2f);
						else
							p.CarrierHome = null;
					}
				}
				carrierAirWing.Clear();
			}

			if (BattleUnitCatalog.IsNavalUnit(UnitKey))
			{
				StartNavalSinkingSequence();
			}
			else if (BattleUnitCatalog.IsRotaryWingAircraft(UnitKey))
			{
				StartHelicopterCrashSequence();
			}
			else if (BattleUnitCatalog.IsAirUnit(UnitKey) && !IsDockedOnCarrier && !parkedAtAirfield && GlobalPosition.Y > 1.2f)
			{
				StartAirCrashSequence();
			}
			else if (!BattleUnitCatalog.IsInfantryLike(UnitKey))
			{
				StartGroundVehicleWreckageSequence();
			}
			else
			{
				StartInfantryDeathSequence();
			}
		}
	}

	void StartNavalSinkingSequence()
	{
		isNavalSinking = true;
		deathDurationSeconds = 6.2f;
		Velocity = Vector3.Zero;

		if (bowWaveParticles is not null && GodotObject.IsInstanceValid(bowWaveParticles))
			bowWaveParticles.Emitting = false;
		if (sternWakeParticles is not null && GodotObject.IsInstanceValid(sternWakeParticles))
			sternWakeParticles.Emitting = false;

		var rng = new Random();
		navalSinkPitchRate = (rng.NextSingle() > 0.5f ? 0.075f : -0.065f);
		navalSinkRollRate = (rng.NextSingle() > 0.5f ? 0.11f : -0.10f);
		navalSinkDepthSpeed = 0.52f;
		navalSecondaryExplosionsFired = 0;

		CombatProjectile.SpawnExplosion(this, GlobalPosition + Vector3.Up * 0.3f, new Color(1.0f, 0.55f, 0.15f), true);
		BattleFeedback.WaterSpout(this, GlobalPosition);
		BattleFeedback.TriggerCameraShake(GlobalPosition, 0.07f, 0.28f);

		SetVisualModulate(new Color(0.24f, 0.24f, 0.24f, 1f));

		var particleQuad = new QuadMesh { Size = new Vector2(0.85f, 0.85f) };
		var particleTex = CombatProjectile.GetRadialGradientTexture();

		navalSinkSmoke = new CpuParticles3D
		{
			Name = "NavalSinkSmoke",
			Amount = 45,
			Lifetime = 2.2f,
			Spread = 20f,
			Gravity = new Vector3(0f, 1.8f, 0f),
			InitialVelocityMin = 0.6f,
			InitialVelocityMax = 2.4f,
			ScaleAmountMin = 0.8f,
			ScaleAmountMax = 3.6f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var smokeMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		navalSinkSmoke.MaterialOverride = smokeMat;
		var smokeScale = new Curve();
		smokeScale.AddPoint(new Vector2(0f, 0.6f));
		smokeScale.AddPoint(new Vector2(1f, 3.8f));
		navalSinkSmoke.ScaleAmountCurve = smokeScale;
		var smokeRamp = CombatProjectile.CreateRamp(
			new Color(0.15f, 0.15f, 0.15f, 0.88f), 0.5f,
			new Color(0.08f, 0.08f, 0.08f, 0.55f),
			new Color(0.04f, 0.04f, 0.04f, 0f));
		navalSinkSmoke.ColorRamp = smokeRamp;
		AddChild(navalSinkSmoke);
		navalSinkSmoke.Position = new Vector3(0f, 0.5f, 0f);
		navalSinkSmoke.Emitting = true;

		var bubbleQuad = new QuadMesh { Size = new Vector2(0.45f, 0.45f) };
		navalSinkBubbles = new CpuParticles3D
		{
			Name = "NavalSinkBubbles",
			Amount = 30,
			Lifetime = 1.2f,
			Spread = 45f,
			Gravity = new Vector3(0f, 0.6f, 0f),
			InitialVelocityMin = 0.4f,
			InitialVelocityMax = 1.6f,
			ScaleAmountMin = 0.5f,
			ScaleAmountMax = 2.0f,
			LocalCoords = false,
			Mesh = bubbleQuad
		};
		var bubbleMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.9f, 0.96f, 1f, 0.8f),
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles
		};
		navalSinkBubbles.MaterialOverride = bubbleMat;
		var bubbleScale = new Curve();
		bubbleScale.AddPoint(new Vector2(0f, 0.4f));
		bubbleScale.AddPoint(new Vector2(1f, 1.8f));
		navalSinkBubbles.ScaleAmountCurve = bubbleScale;
		AddChild(navalSinkBubbles);
		navalSinkBubbles.Position = new Vector3(0f, 0.1f, 0f);
		navalSinkBubbles.Emitting = true;
	}

	void StartHelicopterCrashSequence()
	{
		isHelicopterCrashing = true;
		deathDurationSeconds = 3.6f;
		heliCrashYawVelocity = 14.5f;

		CombatProjectile.SpawnExplosion(this, GlobalPosition + GlobalTransform.Basis.Z * 0.8f, new Color(1.0f, 0.6f, 0.18f), false);
		BattleFeedback.TriggerCameraShake(GlobalPosition, 0.05f, 0.20f);

		var currentDir = Velocity.LengthSquared() > 1f ? Velocity.Normalized() : -GlobalTransform.Basis.Z;
		float curSpeed = Mathf.Clamp(Velocity.Length(), 5f, 14f);
		airCrashVelocity = currentDir * curSpeed + Vector3.Down * 2.0f;

		var particleQuad = new QuadMesh { Size = new Vector2(0.55f, 0.55f) };
		var particleTex = CombatProjectile.GetRadialGradientTexture();

		airCrashSmoke = new CpuParticles3D
		{
			Name = "HeliCrashSmoke",
			Amount = 50,
			Lifetime = 1.2f,
			Spread = 20f,
			Gravity = new Vector3(0f, 1.4f, 0f),
			InitialVelocityMin = 0.5f,
			InitialVelocityMax = 2.0f,
			ScaleAmountMin = 0.5f,
			ScaleAmountMax = 2.5f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var smokeMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		airCrashSmoke.MaterialOverride = smokeMat;
		var smokeScale = new Curve();
		smokeScale.AddPoint(new Vector2(0f, 0.5f));
		smokeScale.AddPoint(new Vector2(1f, 2.8f));
		airCrashSmoke.ScaleAmountCurve = smokeScale;
		var smokeRamp = CombatProjectile.CreateRamp(
			new Color(0.16f, 0.16f, 0.16f, 0.85f), 0.5f,
			new Color(0.08f, 0.08f, 0.08f, 0.55f),
			new Color(0.04f, 0.04f, 0.04f, 0f));
		airCrashSmoke.ColorRamp = smokeRamp;
		AddChild(airCrashSmoke);
		airCrashSmoke.Position = new Vector3(0f, 0.4f, 0.6f);
		airCrashSmoke.Emitting = true;

		airCrashFire = new CpuParticles3D
		{
			Name = "HeliCrashFire",
			Amount = 35,
			Lifetime = 0.32f,
			Spread = 16f,
			Gravity = Vector3.Zero,
			InitialVelocityMin = 1.2f,
			InitialVelocityMax = 3.5f,
			ScaleAmountMin = 0.4f,
			ScaleAmountMax = 1.1f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var fireMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		airCrashFire.MaterialOverride = fireMat;
		var fireRamp = CombatProjectile.CreateRamp(
			new Color(1.0f, 0.68f, 0.12f, 1f), 0.5f,
			new Color(0.95f, 0.32f, 0.02f, 0.85f),
			new Color(0.65f, 0.08f, 0.01f, 0f));
		airCrashFire.ColorRamp = fireRamp;
		AddChild(airCrashFire);
		airCrashFire.Position = new Vector3(0f, 0.3f, 0.4f);
		airCrashFire.Emitting = true;
	}

	void StartAirCrashSequence()
	{
		isAirCrashing = true;
		deathDurationSeconds = 3.5f;
		foreach (var p in jetExhaustParticles)
		{
			if (GodotObject.IsInstanceValid(p))
				p.Emitting = false;
		}

		CombatProjectile.SpawnExplosion(this, GlobalPosition, new Color(1.0f, 0.65f, 0.2f), false);
		BattleFeedback.Impact(this, GlobalPosition, true);

		var currentDir = Velocity.LengthSquared() > 2f ? Velocity.Normalized() : -GlobalTransform.Basis.Z;
		float curSpeed = Mathf.Clamp(Velocity.Length(), 9f, 22f);
		airCrashVelocity = currentDir * curSpeed + Vector3.Down * 3.5f;

		var particleQuad = new QuadMesh { Size = new Vector2(0.55f, 0.55f) };
		var particleTex = CombatProjectile.GetRadialGradientTexture();

		airCrashSmoke = new CpuParticles3D
		{
			Name = "CrashSmokeTrail",
			Amount = 65,
			Lifetime = 1.35f,
			Spread = 16f,
			Gravity = new Vector3(0f, 1.2f, 0f),
			InitialVelocityMin = 0.5f,
			InitialVelocityMax = 2.5f,
			ScaleAmountMin = 0.6f,
			ScaleAmountMax = 2.8f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var smokeMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		airCrashSmoke.MaterialOverride = smokeMat;
		var smokeScale = new Curve();
		smokeScale.AddPoint(new Vector2(0f, 0.5f));
		smokeScale.AddPoint(new Vector2(1f, 3.2f));
		airCrashSmoke.ScaleAmountCurve = smokeScale;
		var smokeRamp = CombatProjectile.CreateRamp(
			new Color(0.18f, 0.18f, 0.18f, 0.85f), 0.5f,
			new Color(0.10f, 0.10f, 0.10f, 0.60f),
			new Color(0.06f, 0.06f, 0.06f, 0f));
		airCrashSmoke.ColorRamp = smokeRamp;
		AddChild(airCrashSmoke);
		airCrashSmoke.Position = new Vector3(0f, 0f, 1.2f);
		airCrashSmoke.Emitting = true;

		airCrashFire = new CpuParticles3D
		{
			Name = "CrashFireTrail",
			Amount = 45,
			Lifetime = 0.35f,
			Spread = 14f,
			Gravity = Vector3.Zero,
			InitialVelocityMin = 1.5f,
			InitialVelocityMax = 4.0f,
			ScaleAmountMin = 0.45f,
			ScaleAmountMax = 1.2f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var fireMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		airCrashFire.MaterialOverride = fireMat;
		var fireRamp = CombatProjectile.CreateRamp(
			new Color(1.0f, 0.70f, 0.15f, 1f), 0.5f,
			new Color(0.96f, 0.36f, 0.03f, 0.85f),
			new Color(0.68f, 0.10f, 0.01f, 0f));
		airCrashFire.ColorRamp = fireRamp;
		AddChild(airCrashFire);
		airCrashFire.Position = new Vector3(0f, 0f, 0.8f);
		airCrashFire.Emitting = true;
	}

	void StartGroundVehicleWreckageSequence()
	{
		isGroundWreckage = true;
		deathDurationSeconds = 5.6f;
		Velocity = Vector3.Zero;

		SetVisualModulate(new Color(0.18f, 0.18f, 0.18f, 1f));

		if (weaponYawNode is not null && GodotObject.IsInstanceValid(weaponYawNode) && BattleUnitCatalog.IsTankLike(UnitKey))
		{
			turretEjected = true;
			var rng = new Random();
			float vx = (rng.NextSingle() - 0.5f) * 1.8f;
			float vz = (rng.NextSingle() - 0.5f) * 1.8f;
			turretVelocity = new Vector3(vx, 4.6f, vz);
			turretRotVelocity = new Vector3(3.4f, (rng.NextSingle() - 0.5f) * 4.5f, 2.2f);
		}

		CombatProjectile.SpawnExplosion(this, GlobalPosition + Vector3.Up * 0.45f, new Color(1.0f, 0.5f, 0.12f), true);
		BattleFeedback.Destroyed(this, GlobalPosition, true);

		groundScorchDecal = new MeshInstance3D
		{
			Mesh = new PlaneMesh { Size = new Vector2(3.0f, 3.0f) },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.14f, 0.12f, 0.10f, 0.55f),
				AlbedoTexture = CombatProjectile.GetRadialGradientTexture(),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
				DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
			},
			Position = new Vector3(0f, 0.02f, 0f)
		};
		AddChild(groundScorchDecal);

		var particleQuad = new QuadMesh { Size = new Vector2(0.6f, 0.6f) };
		var particleTex = CombatProjectile.GetRadialGradientTexture();

		wreckageSmoke = new CpuParticles3D
		{
			Name = "WreckageSmoke",
			Amount = 40,
			Lifetime = 2.0f,
			Spread = 22f,
			Gravity = new Vector3(0f, 1.5f, 0f),
			InitialVelocityMin = 0.4f,
			InitialVelocityMax = 1.8f,
			ScaleAmountMin = 0.6f,
			ScaleAmountMax = 3.2f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var smokeMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		wreckageSmoke.MaterialOverride = smokeMat;
		var smokeScale = new Curve();
		smokeScale.AddPoint(new Vector2(0f, 0.5f));
		smokeScale.AddPoint(new Vector2(1f, 3.2f));
		wreckageSmoke.ScaleAmountCurve = smokeScale;
		var smokeRamp = CombatProjectile.CreateRamp(
			new Color(0.14f, 0.14f, 0.14f, 0.88f), 0.5f,
			new Color(0.08f, 0.08f, 0.08f, 0.55f),
			new Color(0.04f, 0.04f, 0.04f, 0f));
		wreckageSmoke.ColorRamp = smokeRamp;
		AddChild(wreckageSmoke);
		wreckageSmoke.Position = new Vector3(0f, 0.35f, 0f);
		wreckageSmoke.Emitting = true;

		wreckageFire = new CpuParticles3D
		{
			Name = "WreckageFire",
			Amount = 25,
			Lifetime = 0.45f,
			Spread = 18f,
			Gravity = new Vector3(0f, 0.8f, 0f),
			InitialVelocityMin = 0.6f,
			InitialVelocityMax = 2.2f,
			ScaleAmountMin = 0.4f,
			ScaleAmountMax = 1.1f,
			LocalCoords = false,
			Mesh = particleQuad
		};
		var fireMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = Colors.White,
			AlbedoTexture = particleTex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true
		};
		wreckageFire.MaterialOverride = fireMat;
		var fireRamp = CombatProjectile.CreateRamp(
			new Color(1.0f, 0.65f, 0.12f, 1f), 0.6f,
			new Color(0.92f, 0.30f, 0.02f, 0.8f),
			new Color(0.55f, 0.06f, 0.01f, 0f));
		wreckageFire.ColorRamp = fireRamp;
		AddChild(wreckageFire);
		wreckageFire.Position = new Vector3(0f, 0.25f, 0f);
		wreckageFire.Emitting = true;
	}

	void StartInfantryDeathSequence()
	{
		deathDurationSeconds = 2.8f;
		Velocity = Vector3.Zero;
		BattleFeedback.Destroyed(this, GlobalPosition, false);

		if (visualAnimationPlayers.Count > 0)
		{
			foreach (var player in visualAnimationPlayers)
			{
				if (player.HasAnimation("death"))
					player.Play("death");
				else if (player.HasAnimation("die"))
					player.Play("die");
				else if (!string.IsNullOrEmpty(hitAnimationName) && player.HasAnimation(hitAnimationName))
					player.Play(hitAnimationName);
			}
		}
	}

	void DetachAndFadeCrashParticles()
	{
		var parent = GetParent();
		void FadeOutAndFree(Node3D? node, float fadeTime)
		{
			if (node is null || !GodotObject.IsInstanceValid(node))
				return;
			if (node is CpuParticles3D p)
				p.Emitting = false;

			node.GetParent()?.RemoveChild(node);
			if (parent is not null && GodotObject.IsInstanceValid(parent))
			{
				parent.AddChild(node);
				var tw = node.CreateTween();
				tw.TweenInterval(fadeTime);
				tw.TweenCallback(Callable.From(node.QueueFree));
			}
			else
			{
				node.QueueFree();
			}
		}

		FadeOutAndFree(airCrashSmoke, 1.8f);
		airCrashSmoke = null;
		FadeOutAndFree(airCrashFire, 0.5f);
		airCrashFire = null;

		FadeOutAndFree(navalSinkSmoke, 2.2f);
		navalSinkSmoke = null;
		FadeOutAndFree(navalSinkBubbles, 1.5f);
		navalSinkBubbles = null;

		FadeOutAndFree(wreckageSmoke, 2.4f);
		wreckageSmoke = null;
		FadeOutAndFree(wreckageFire, 0.6f);
		wreckageFire = null;
		FadeOutAndFree(groundScorchDecal, 3.5f);
		groundScorchDecal = null;
	}

	public void OnDamagedBy(Node3D attacker)
	{
		if (IsDead || !GodotObject.IsInstanceValid(attacker) || !IsLiveEnemyTarget(attacker))
			return;

		// 1. 若当前处于空闲、目标已阵亡或无有效目标：只要本单位有对该目标开火的能力，立刻转向反击偷袭者！
		//    坦克绝不能攻击空中飞机，因此若被敌机轰炸，坦克绝不锁定敌机，而是由防空同伴接管对空防御！
		if (CanAttackTarget(attacker))
		{
			if (AttackTarget is null || !IsLiveEnemyTarget(AttackTarget) || IsIdle())
			{
				Attack(attacker);
			}
			else if (AttackTarget != attacker)
			{
				// 2. 若已有目标，但袭扰者近在眼前（高危贴脸威胁）：紧急切火自卫
				float currentDistSq = GlobalPosition.DistanceSquaredTo(AttackTarget.GlobalPosition);
				float attackerDistSq = GlobalPosition.DistanceSquaredTo(attacker.GlobalPosition);
				if (attackerDistSq < currentDistSq * 0.40f && attackerDistSq < AttackRange * AttackRange)
				{
					Attack(attacker);
				}
			}
		}

		// 3. 呼叫周围 18 米内空闲同伴协同聚焦反击（防空炮等能打飞机的友军会收到警报并集火空中敌机）
		AlertNearbyAllies(attacker);
	}

	void AlertNearbyAllies(Node3D attacker)
	{
		if (BattleGameManager.Instance is not { } mgr) return;
		float alertRadiusSq = 18f * 18f;
		var myPos = GlobalPosition;
		foreach (var ally in mgr.GetUnits(PlayerOwned))
		{
			if (ally == this || !GodotObject.IsInstanceValid(ally) || ally.IsDead) continue;
			// 仅呼叫具备打击该目标能力的友军（例如呼叫防空炮打飞机，地面坦克绝不响应对空反击）
			if (!ally.CanAttackTarget(attacker)) continue;
			if (ally.AttackTarget is null && ally.IsIdle() && !ally.holdPosition)
			{
				if (ally.GlobalPosition.DistanceSquaredTo(myPos) <= alertRadiusSq)
				{
					ally.Attack(attacker);
				}
			}
		}
	}

	public float Repair(float amount, bool showFeedback = true)
	{
		if (IsDead || amount <= 0f)
			return 0f;

		var before = Health;
		Health = Mathf.Min(MaxHealth, Health + amount);
		var repaired = Health - before;
		if (showFeedback)
			BattleFeedback.Repair(this, GlobalPosition, repaired);
		return repaired;
	}

	public void SetHealthFraction(float ratio)
	{
		if (IsDead)
			return;

		Health = Mathf.Clamp(MaxHealth * Mathf.Clamp(ratio, 0.01f, 1f), 1f, MaxHealth);
		RefreshCombatOverlays();
	}

	public void ApplyTechBuff(
		string sourceId,
		float duration,
		float moveMultiplier,
		float damageMultiplier,
		float attackRangeBonus,
		float attackCooldownMultiplier,
		float defenseReduction,
		float visionBonus,
		float regenPerSecond,
		Color tint)
	{
		CaptureBaseStats();
		techBuffs.RemoveAll(buff => buff.SourceId == sourceId);
		techBuffs.Add(new RuntimeTechBuff
		{
			SourceId = sourceId,
			Remaining = Mathf.Max(0.1f, duration),
			MoveMultiplier = Mathf.Max(0.01f, moveMultiplier),
			DamageMultiplier = Mathf.Max(0.01f, damageMultiplier),
			AttackRangeBonus = attackRangeBonus,
			AttackCooldownMultiplier = Mathf.Max(0.01f, attackCooldownMultiplier),
			DefenseReduction = Mathf.Max(0f, defenseReduction),
			VisionBonus = visionBonus,
			RegenPerSecond = Mathf.Max(0f, regenPerSecond),
			Tint = tint
		});
		RecalculateTechBuffStats();
	}

	public void ConfigureVisualRig(string unitKey, Node3D? root)
	{
		visualKey = unitKey ?? "";
		bodyFacingYawOffset = 0f;
		visualRoot = root;
		visualRootRestPosition = root is not null ? root.Position : Vector3.Zero;
		baseVisualRoll = root is not null ? root.Rotation.Z : 0f;
		visualBaseBasis = root is not null ? root.Basis : Basis.Identity;
		hitFlashTimer = 0f;
		hitImpulseOffset = Vector3.Zero;
		hitImpulseVelocity = Vector3.Zero;
		hitTiltAngle = 0f;
		hitTiltVelocity = 0f;
		weaponRecoilVelocity = 0f;
		chassisRecoilPitch = 0f;
		chassisRecoilPitchVel = 0f;
		weaponYawNode = null;
		weaponPitchNode = null;
		weaponPitchIsPivot = false;
		weaponYawLookOffset = Vector3.Zero;
		weaponPitchLookOffset = Vector3.Zero;
		weaponPitchPivotOffset = Vector3.Zero;
		visualAnimationPlayer = null;
		visualAnimationPlayers.Clear();
		walkAnimationName = "";
		runAnimationName = "";
		idleAnimationName = "";
		fireAnimationName = "";
		hitAnimationName = "";
		reloadAnimationName = "";
		grenadeAnimationName = "";
		currentVisualAnimationName = "";
		visualAnimationLockRemaining = 0f;
		crouchRatio = 0f;
		infantryVisualParts.Clear();
		propellerNodes.Clear();
		helicopterTailRotor = null;

		if (root is null || root is RtsUnit)
			return;

		if (unitKey == "scout_plane" || unitKey == "scout_helicopter")
		{
			helicopterTailRotor = root.FindChild("TailRotorRig", true, false) as Node3D;
		}

		visualAnimationPlayers.AddRange(root.FindChildren("*", "AnimationPlayer", true, false)
			.OfType<AnimationPlayer>()
			.ToArray());
		visualAnimationPlayer = visualAnimationPlayers.FirstOrDefault();
		ResolveVisualAnimationNames();

		if ((unitKey == "scout_plane" || unitKey == "scout_helicopter") && visualAnimationPlayers.Count > 0)
		{
			foreach (var player in visualAnimationPlayers)
			{
				foreach (var animName in player.GetAnimationList())
				{
					var anim = player.GetAnimation(animName);
					if (anim is null) continue;

					// 剔除骨架对机身全局位移/抬升的关键帧（保留主旋翼 Blades 与尾翼 Screw 的旋转驱动）
					for (int i = anim.GetTrackCount() - 1; i >= 0; i--)
					{
						var pathStr = anim.TrackGetPath(i).ToString();
						if (pathStr.EndsWith(":Helicopter") || pathStr.Contains("Helicopter"))
						{
							anim.RemoveTrack(i);
						}
					}
					anim.LoopMode = Animation.LoopModeEnum.Linear;
					idleAnimationName = animName.ToString();
					walkAnimationName = animName.ToString();
					runAnimationName = animName.ToString();
					player.Play(animName.ToString());
				}
			}
		}

		if (unitKey == "aircraft_carrier" || root.Name.ToString().Contains("Nimitz", System.StringComparison.OrdinalIgnoreCase))
		{
			var wakeChild = root.FindChild("Wake", true, false) as Node3D;
			if (wakeChild != null)
			{
				wakeChild.Visible = false;
			}
			foreach (var node in root.FindChildren("*Wake*", "Node3D", true, false))
			{
				if (node is Node3D n3d)
				{
					n3d.Visible = false;
				}
			}
		}

		switch (unitKey)
		{
			case "tank_destroyer":
				weaponYawNode = root.FindChild("Turret_Main", true, false) as Node3D ?? root;
				weaponPitchNode = root.FindChild("Gun_Mantlet", true, false) as Node3D
					?? root.FindChild("Main_Cannon_Barrel", true, false) as Node3D;
				weaponYawLookOffset = Vector3.Zero;
				weaponPitchLookOffset = Vector3.Zero;
				break;
			case "heavy_tank":
				weaponYawNode = root.FindChild("misc_a", true, false) as Node3D
					?? root.FindChild("mount2", true, false) as Node3D
					?? root;
				weaponPitchNode = root.FindChild("misc_b", true, false) as Node3D
					?? root.FindChild("mount2", true, false) as Node3D
					?? root.FindChild("weapon", true, false) as Node3D;
				weaponYawLookOffset = Vector3.Zero;
				weaponPitchLookOffset = Vector3.Zero;
				break;
			case "light_tank":
				weaponYawNode = root.FindChild("base", true, false) as Node3D ?? root;
				weaponPitchNode = root.FindChild("gun_elevate", true, false) as Node3D
					?? root.FindChild("coax", true, false) as Node3D;
				// 轻型坦克主炮沿 +Z 建模，炮塔瞄准附加 180° Y 轴偏置，使主炮朝向与目标对齐
				weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
				weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
				break;
			case "tank":
			case "medium_tank":
				weaponYawNode = root.FindChild("turret_exterior", true, false) as Node3D
					?? root.FindChild("mantlet_inner", true, false) as Node3D
					?? root;
				weaponPitchNode = root.FindChild("barrel", true, false) as Node3D
					?? root.FindChild("gun", true, false) as Node3D;
				weaponPitchIsPivot = false;
				HidePanzerInteriorVisuals(root);
				PreparePanzerTurretRig(root);
				weaponYawNode = root.FindChild("TurretPivot", true, false) as Node3D ?? weaponYawNode;
				weaponPitchNode = root.FindChild("GunPivot", true, false) as Node3D ?? weaponPitchNode;
				if (weaponPitchNode == root || weaponPitchNode == weaponYawNode)
					weaponPitchNode = null;
				// 坦克主炮在 pzIV.glb 模型中沿 +Z 建模，瞄准朝向附加 180° Y 轴偏置，使主炮与目标严格对齐
				weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
				weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
				break;
			case "artillery":
				weaponYawNode = null;
				PrepareArtilleryGunRig(root);
				weaponPitchNode = root.GetNodeOrNull<Node3D>("GunPivot")
					?? root.FindChild("GunPivot", true, false) as Node3D
					?? root;
				weaponPitchIsPivot = weaponPitchNode.Name == "GunPivot";
				weaponPitchPivotOffset = Vector3.Zero;
				break;
			case "anti_air_gun":
			case "sam_missile":
				HidePanzerInteriorVisuals(root);
				PreparePanzerTurretRig(root);
				weaponYawNode = root.FindChild("vehicle#bone_turret_90", true, false) as Node3D
					?? root.FindChild("TurretPivot", true, false) as Node3D
					?? root.FindChild("cannon", true, false) as Node3D
					?? root.FindChild("weapon", true, false) as Node3D
					?? root;
				weaponPitchNode = root.FindChild("vehicle#gun_barrel_01_101", true, false) as Node3D
					?? root.FindChild("vehicle#gun_01_98", true, false) as Node3D
					?? root.FindChild("GunPivot", true, false) as Node3D
					?? root.FindChild("barrel", true, false) as Node3D
					?? root.FindChild("gun", true, false) as Node3D
					?? weaponYawNode;
				if (weaponPitchNode == root || weaponPitchNode == weaponYawNode)
					weaponPitchNode = null;
				weaponYawLookOffset = new Vector3(0f, Mathf.Pi, 0f);
				weaponPitchLookOffset = new Vector3(0f, Mathf.Pi, 0f);
				break;
		}

		CaptureInfantryVisualParts(root);
		CapturePropellerNodes(root);
		FixVehicleMeshMaterials(root, PlayerOwned ? new Color(0.24f, 0.45f, 0.65f) : new Color(0.65f, 0.28f, 0.25f));

		if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode) && weaponPitchNode != this)
		{
			weaponPitchRestPosition = weaponPitchNode.Position;
			hasRecordedWeaponPitchRest = true;
		}
		else
		{
			weaponPitchRestPosition = Vector3.Zero;
			hasRecordedWeaponPitchRest = false;
		}
	}

	public void SetSelected(bool value)
	{
		Selected = value;
		if (selectionRing is not null)
		{
			selectionRing.Visible = value && (PlayerOwned || FogRevealed);
			if (value)
			{
				selectionRing.Scale = SelectionRingScale() * 1.15f;
				var tw = selectionRing.CreateTween();
				tw.TweenProperty(selectionRing, "scale", SelectionRingScale(), 0.14)
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
			}
		}
		if (groundShadowRing is not null)
		{
			groundShadowRing.Visible = value && BattleUnitCatalog.IsAirUnit(UnitKey) && (PlayerOwned || FogRevealed);
		}
	}

	public void SetFogRevealed(bool revealed)
	{
		bool wasRevealed = FogRevealed;
		FogRevealed = PlayerOwned || revealed;
		if (FogRevealed)
			FogExplored = true;

		if (!PlayerOwned && !wasRevealed && FogRevealed)
		{
			OnEnemySpotted();
		}

		if (wasRevealed == FogRevealed && (PlayerOwned || Visible == FogRevealed))
			return;

		if (PlayerOwned)
		{
			// 己方单位始终完全显示
			Visible = true;
			CollisionLayer = BattleUnitCatalog.IsAirUnit(UnitKey) ? 8u : originalCollisionLayer;
			CollisionMask = BattleUnitCatalog.IsAirUnit(UnitKey) ? 0u : originalCollisionMask;
		}
		else if (FogRevealed)
		{
			// 敌方单位在视野内：完全可见
			Visible = true;
			SetVisualModulate(Colors.White);
			CollisionLayer = BattleUnitCatalog.IsAirUnit(UnitKey) ? 8u : originalCollisionLayer;
			CollisionMask = BattleUnitCatalog.IsAirUnit(UnitKey) ? 0u : originalCollisionMask;
		}
		else
		{
			// 敌方单位不在视野内：完全隐藏（移动单位在无视野时不可见）
			Visible = false;
			SetVisualModulate(Colors.White);
			CollisionLayer = 0;
			CollisionMask = 0;
			SetSelected(false);
		}
	}

	float lastSpottedAlertTime = -999f;

	void OnEnemySpotted()
	{
		if (Godot.Time.GetTicksMsec() < 1500)
			return;

		var now = (float)(Godot.Time.GetTicksMsec() / 1000.0);
		if (now - lastSpottedAlertTime < 12.0f)
			return;

		lastSpottedAlertTime = now;
		BattleMinimap.Instance?.NotifyEnemySpotted(GlobalPosition, DisplayName, false);
	}

	void ProcessMove(float delta)
	{
		var flatDelta = TargetPosition - GlobalPosition;
		flatDelta.Y = 0f;
		bool isNaval = BattleUnitCatalog.IsNavalUnit(UnitKey);
		bool isVehicle = !isNaval && !BattleUnitCatalog.IsInfantryLike(UnitKey) && !BattleUnitCatalog.IsAirUnit(UnitKey);
		float arrivalThreshold = BattleUnitCatalog.IsAirUnit(UnitKey) ? 5.2f : (isVehicle ? 0.35f : 0.20f);
		if (flatDelta.Length() <= arrivalThreshold)
		{
			if (queuedMoveTargets.Count > 0)
			{
				var next = queuedMoveTargets.Dequeue();
				if (BattleUnitCatalog.IsAirUnit(UnitKey) && isAirPatrolCircuit && !parkingAtAirfield && !AttackMoving && AttackTarget == null)
				{
					// 空中战机巡逻闭环：将经过的航路点重新追加到队尾，形成平滑循环飞行的无缝巡航大环线
					queuedMoveTargets.Enqueue(TargetPosition);
				}
				TargetPosition = next;
				return;
			}

			if (parkingAtAirfield)
			{
				AttackMoving = false;
				isManualMoveOrder = false;
				Velocity = Vector3.Zero;
				MaintainMovementLayer();
				ParkAtAirfield();
				return;
			}

			if (BattleUnitCatalog.IsFixedWingAircraft(UnitKey))
			{
				// 固定翼战机空中绝无静止悬停动力学：到达目标后自动顺切入高空 26~32 米大半径盘旋待机环线 (Loiter Orbit)
				AttackMoving = false;
				isManualMoveOrder = false;
				StartAirLoiterOrbit(TargetPosition);
				return;
			}

			AttackMoving = false;
			isManualMoveOrder = false;
			Velocity = Vector3.Zero;
			if (!BattleUnitCatalog.IsAirUnit(UnitKey))
				MoveAndSlide();
			MaintainMovementLayer();
			return;
		}

		var direction = flatDelta.Normalized();

		if (isNaval)
		{
			// 大型水面战舰动力学约束：持续平滑水动力流体推进，平滑弧线转向，绝不原地急停或卡顿突跳
			var shipForward = -GlobalTransform.Basis.Z;
			shipForward.Y = 0f;
			if (shipForward.LengthSquared() > 0.0001f)
				shipForward = shipForward.Normalized();
			else
				shipForward = direction;

			FaceDirection(direction, delta);
			float dot = shipForward.Dot(direction);
			float speedFactor = Mathf.Clamp(dot * 0.5f + 0.5f, 0.45f, 1.0f);
			Velocity = shipForward * (MoveSpeed * speedFactor);
		}
		else if (isVehicle)
		{
			// 履带式/轮式装甲战车动力学约束 (Tracked Vehicle Kinematics)
			// 1. 严格禁止侧向侧滑漂移 (Zero Lateral Slip): 线速度方向严格与车首朝向 (-Basis.Z) 同轴共线
			// 2. 原地差速转向与连续油门过渡 (Smooth Pivot-to-Cruise Blending):
			// 彻底消除在临界阈值附近的硬切跳变与速度急刹抖动 (Anti-Chattering)
			var chassisForward = -GlobalTransform.Basis.Z;
			chassisForward.Y = 0f;
			if (chassisForward.LengthSquared() > 0.0001f)
				chassisForward = chassisForward.Normalized();
			else
				chassisForward = direction;

			float dot = chassisForward.Dot(direction);

			// 仅当距离目标点较远时才纠正朝向，避免在目标点正下方时产生极性反转打转
			if (flatDelta.LengthSquared() > 0.30f * 0.30f)
			{
				FaceDirection(direction, delta);
			}

			if (dot <= 0.35f)
			{
				// 航向夹角 > 69°（掉头或大急转弯）：双履带纯原地差速回转，前进线速度平滑归零，严禁侧向漂移
				float curSpeed = Mathf.Max(0f, Velocity.Dot(chassisForward));
				float nextSpeed = Mathf.MoveToward(curSpeed, 0f, MoveSpeed * 5.0f * delta);
				Velocity = chassisForward * nextSpeed;
			}
			else
			{
				// 航向夹角 <= 69°：根据对准度执行平滑油门加速，严格沿车身纵轴 (-Basis.Z) 输出标量前进速度
				float alignProgress = Mathf.Clamp((dot - 0.35f) / 0.65f, 0f, 1.0f);
				float smoothSpeedFactor = alignProgress * alignProgress; // 平滑二次缓入曲线
				float curSpeed = Mathf.Max(0f, Velocity.Dot(chassisForward));
				float targetSpeed = MoveSpeed * smoothSpeedFactor;
				float forwardObstacleFactor = GetForwardObstacleTrafficFactor(chassisForward);
				targetSpeed *= forwardObstacleFactor;
				float nextSpeed = Mathf.MoveToward(curSpeed, targetSpeed, MoveSpeed * 4.5f * delta);
				Velocity = chassisForward * nextSpeed;
			}
		}
		else if (BattleUnitCatalog.IsFixedWingAircraft(UnitKey))
		{
			// 固定翼战机空气动力学物理约束 (Fixed-Wing Aerodynamic Kinematics):
			// 1. 速度矢量严格与机头方向 (-Basis.Z) 同向共线，绝无侧滑倒退、倒车或侧滑翻转
			// 2. 航向通过真实气动盘旋转向角速度平滑对齐，在空中持续向前飞并拉出大半径回转圆弧 (Continuous Curved Flight Arcs)，彻底杜绝原地掉头
			// 3. 飞行速度持续保持前向巡航动力，即使掉头也必须向前划大半圆弧机动
			var planeForward = -GlobalTransform.Basis.Z;
			planeForward.Y = 0f;
			if (planeForward.LengthSquared() > 0.0001f)
				planeForward = planeForward.Normalized();
			else
				planeForward = direction;

			FaceDirection(direction, delta);

			// 迎角/航向对准度 (Heading Alignment Dot):
			// 若目标在机尾方向 (dot < 0)，战机继续向前飞并大坡度拉转弯弧线，速度平滑受控；机头对准目标时全速冲刺推进
			float dot = planeForward.Dot(direction);
			float speedFactor = Mathf.Clamp(dot * 0.35f + 0.65f, 0.65f, 1.0f);
			Velocity = planeForward * (MoveSpeed * speedFactor);
		}
		else
		{
			Velocity = direction * MoveSpeed;
			FaceDirection(direction, delta);
		}

		var isLandUnit = !BattleUnitCatalog.IsAirUnit(UnitKey) && !BattleUnitCatalog.IsNavalUnit(UnitKey);
		var isNavalUnit = BattleUnitCatalog.IsNavalUnit(UnitKey);
		if (BattleGameManager.Instance is { } manager && !BattleGameManager.DisableWaterCheck)
		{
			var nextPos = GlobalPosition + Velocity * delta;
			if (isLandUnit && manager.IsWaterPoint(nextPos, 0.2f))
			{
				Velocity = Vector3.Zero;
				var safeLand = manager.ClosestLandPoint(GlobalPosition, 2.0f);
				GlobalPosition = new Vector3(safeLand.X, GlobalPosition.Y, safeLand.Z);
				TargetPosition = GlobalPosition;
				queuedMoveTargets.Clear();
				MoveAndSlide();
				MaintainMovementLayer();
				return;
			}
			else if (isNavalUnit && !manager.IsWaterPoint(nextPos, 0f))
			{
				Velocity = Vector3.Zero;
				float navalInset = (UnitKey == "aircraft_carrier" || UnitKey == "battleship") ? 4.5f : 2.2f;
				var safeWater = manager.ClosestWaterPoint(GlobalPosition, navalInset);
				GlobalPosition = new Vector3(safeWater.X, GlobalPosition.Y, safeWater.Z);
				TargetPosition = GlobalPosition;
				queuedMoveTargets.Clear();
				MoveAndSlide();
				MaintainMovementLayer();
				return;
			}
		}

		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			// 空军战机绝不发生物理刚体碰撞阻挡，直接基于平滑速度位移推进
			GlobalPosition += Velocity * delta;
			MaintainMovementLayer();
			return;
		}

		MoveAndSlide();
		MaintainMovementLayer();
	}

	void ProcessAttack(float delta)
	{
		if (IsReturningToCarrier)
		{
			ReturnToCarrierBase();
			ProcessMove(delta);
			return;
		}

		if (BattleUnitCatalog.IsAirUnit(UnitKey) && CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead && ShouldReturnForCarrier())
		{
			AttackTarget = null;
			ReturnToCarrierBase();
			ProcessMove(delta);
			return;
		}

		if (AttackTarget is null || !IsLiveEnemyTarget(AttackTarget) || !CanAttackTarget(AttackTarget))
		{
			AttackTarget = null;
			if (Patrolling)
				TargetPosition = patrolHeadingToB ? patrolB : patrolA;
			else if (Guarding)
				TargetPosition = NormalizeCommandPosition(GuardFollowPosition());
			else if (BattleGameManager.Instance is { } mgr)
			{
				// 目标阵亡后自动在射程圈周边检索新目标，保持战斗连续性
				float searchR = UnitKey == "aircraft_carrier" ? BattleUnitCatalog.CarrierAirStrikeRange : AttackRange * 1.6f;
				var nextTarget = FindBestTarget(mgr, searchR);
				if (nextTarget is not null)
					AttackTarget = nextTarget;
			}
			if (AttackTarget is null)
			{
				if (CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead)
				{
					ReturnToCarrierBase();
				}
				return;
			}
		}

		var targetPos = AttackTarget.GlobalPosition;
		var toTarget = targetPos - GlobalPosition;
		toTarget.Y = 0f;
		float dist = toTarget.Length();

		// 空中战机（战斗机/轰炸机/侦察机）：执行高速俯冲扫射、投弹与盘旋回路，绝不空中刹停发呆
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			ProcessAirCombat(delta, targetPos, toTarget, dist);
			return;
		}

		float effectiveAttackRange = (SupportsArtilleryDeploy && DeployState == ArtilleryDeployState.Deployed)
			? AttackRange * BattleUnitCatalog.DeployedRangeMultiplier(UnitKey)
			: (UnitKey == "aircraft_carrier" ? BattleUnitCatalog.CarrierAirStrikeRange : AttackRange);

		// 地面与舰艇：超出射程时向弧形战术阵位推进
		if (dist > effectiveAttackRange)
		{
			// 如果是手动移动指令 (MoveTo)，且目标已脱离射程：不再放弃行军路线转头去追，而是清空目标继续向目标点行进
			if (isManualMoveOrder)
			{
				AttackTarget = null;
				ProcessMove(delta);
				return;
			}

			if (SupportsArtilleryDeploy && (DeployState == ArtilleryDeployState.Deployed || DeployState == ArtilleryDeployState.Deploying))
			{
				BeginArtilleryUndeploy();
				return;
			}

			var approachVec = GlobalPosition - targetPos;
			approachVec.Y = 0f;
			if (approachVec.LengthSquared() < 0.04f)
				approachVec = Vector3.Back;

			var approachDir = approachVec.Normalized();
			if (Mathf.Abs(attackSlotAngleOffset) > 0.001f)
				approachDir = approachDir.Rotated(Vector3.Up, attackSlotAngleOffset);

			float idealDist = UnitKey == "aircraft_carrier"
				? BattleUnitCatalog.CarrierStandoffDistance
				: Mathf.Max(3.0f, effectiveAttackRange * 0.78f);
			var standoffPoint = targetPos + approachDir * idealDist;
			TargetPosition = NormalizeCommandPosition(standoffPoint);

			// 侧翼防拥堵避让微调
			ApplyFlankSeparationSteering();
			ProcessMove(delta);
			// 关键修复：推进途中炮塔提前锁定瞄准，实现迎敌即打
			UpdateWeaponAim(targetPos, false, delta);
			return;
		}

		if (SupportsArtilleryDeploy)
		{
			if (DeployState == ArtilleryDeployState.Mobile)
			{
				Velocity = Vector3.Zero;
				MoveAndSlide();
				MaintainMovementLayer();
				BeginArtilleryDeploy();
				return;
			}
			if (DeployState == ArtilleryDeployState.Deploying)
			{
				Velocity = Vector3.Zero;
				MoveAndSlide();
				MaintainMovementLayer();
				return;
			}
			if (DeployState == ArtilleryDeployState.Undeploying)
			{
				BeginArtilleryDeploy();
				Velocity = Vector3.Zero;
				MoveAndSlide();
				MaintainMovementLayer();
				return;
			}
		}

		// 游走打击与行进间动态射击 (Dynamic Fire-on-the-Move & Skirmish)
		float minSafetyDist = UnitKey == "aircraft_carrier"
			? BattleUnitCatalog.CarrierStandoffDistance - 10.0f
			: Mathf.Max(3.2f, AttackRange * 0.45f);
		float idealCombatDist = UnitKey == "aircraft_carrier"
			? BattleUnitCatalog.CarrierStandoffDistance
			: Mathf.Max(3.5f, effectiveAttackRange * 0.78f);
		bool isInfantry = BattleUnitCatalog.IsInfantryLike(UnitKey);
		bool isVehicle = !BattleUnitCatalog.IsNavalUnit(UnitKey) && !isInfantry && !BattleUnitCatalog.IsAirUnit(UnitKey);
		bool hasExplicitMoveOrder = !IsIdle() && isManualMoveOrder;

		if (CanFireWhileMoving)
		{
			// 炮塔独立 360 度伺服跟踪目标开火，无论底盘在前进、倒车还是巡航，炮口始终锁死敌方
			UpdateWeaponAim(targetPos, false, delta);

			if (hasExplicitMoveOrder)
			{
				// 1. 行进间动态射击 (Fire-on-the-move):
				// 拥有显式右键地面行军任务的坦克和战车，底盘继续全速行军推进，绝不急刹停顿！
				ProcessMove(delta);
			}
			else if (!holdPosition)
			{
				// 2. 具备行进间射击能力的坦克/战车在交火攻击时：
				// 绝不在踏入射程瞬间原地刹死，保持现代装甲作战行进射击与动态机动 (Dynamic Combat Locomotion)
				if (dist < minSafetyDist && AttackRange >= 7.0f)
				{
					// 贴脸近身：保持车头迎敌，履带纯倒车后退拉扯开火
					var chassisForward = -GlobalTransform.Basis.Z;
					chassisForward.Y = 0f;
					chassisForward = chassisForward.LengthSquared() > 0.0001f ? chassisForward.Normalized() : toTarget.Normalized();

					Velocity = -chassisForward * (MoveSpeed * 0.60f);
					if (BattleGameManager.Instance is { } mgr && !BattleGameManager.DisableWaterCheck)
					{
						var nextPos = GlobalPosition + Velocity * delta;
						if (!BattleUnitCatalog.IsNavalUnit(UnitKey) && mgr.IsWaterPoint(nextPos, 0.3f))
							Velocity = Vector3.Zero;
					}
					MoveAndSlide();
					MaintainMovementLayer();
				}
				else if (dist > idealCombatDist + 0.6f)
				{
					// 刚刚踏入最大射程圈边缘，但尚未抵达理想战术射击阵位：
					// 底盘继续向战术阵位平滑推进，边推进边开火，绝不在最大射程线急停卡壳！
					var approachVec = GlobalPosition - targetPos;
					approachVec.Y = 0f;
					if (approachVec.LengthSquared() < 0.04f)
						approachVec = Vector3.Back;
					var approachDir = approachVec.Normalized();
					if (Mathf.Abs(attackSlotAngleOffset) > 0.001f)
						approachDir = approachDir.Rotated(Vector3.Up, attackSlotAngleOffset);

					var standoffPoint = targetPos + approachDir * idealCombatDist;
					TargetPosition = NormalizeCommandPosition(standoffPoint);
					ApplyFlankSeparationSteering();
					ProcessMove(delta);
				}
				else if (AttackTarget is RtsUnit enemyUnit && enemyUnit.Velocity.LengthSquared() > 0.35f && dist > minSafetyDist + 1.0f && enemyUnit.Velocity.Dot(toTarget) > 0.2f)
				{
					// 仅当敌方目标正在撤退/远离时，我方底盘才执行跟随巡航追击；若敌方迎面冲来，绝不迎头对撞
					var chaseDir = toTarget.Normalized();
					if (isVehicle)
					{
						// 装甲战车动力学约束：沿车身首向平滑加减速，大角度优先原地差速转正，杜绝横向侧移
						var chassisForward = -GlobalTransform.Basis.Z;
						chassisForward.Y = 0f;
						chassisForward = chassisForward.LengthSquared() > 0.0001f ? chassisForward.Normalized() : chaseDir;

						FaceDirection(chaseDir, delta);
						float dot = chassisForward.Dot(chaseDir);
						if (dot > 0.35f)
						{
							float alignProgress = Mathf.Clamp((dot - 0.35f) / 0.65f, 0f, 1.0f);
							float speedFactor = alignProgress * alignProgress;
							float curSpeed = Mathf.Max(0f, Velocity.Dot(chassisForward));
							float nextSpeed = Mathf.MoveToward(curSpeed, MoveSpeed * 0.70f * speedFactor, MoveSpeed * 4.5f * delta);
							Velocity = chassisForward * nextSpeed;
						}
						else
						{
							float curSpeed = Mathf.Max(0f, Velocity.Dot(chassisForward));
							float nextSpeed = Mathf.MoveToward(curSpeed, 0f, MoveSpeed * 5.0f * delta);
							Velocity = chassisForward * nextSpeed;
						}
					}
					else
					{
						Velocity = chaseDir * (MoveSpeed * 0.70f);
						FaceDirection(chaseDir, delta);
					}

					if (BattleGameManager.Instance is { } mgr && !BattleGameManager.DisableWaterCheck)
					{
						var nextPos = GlobalPosition + Velocity * delta;
						if (!BattleUnitCatalog.IsNavalUnit(UnitKey) && mgr.IsWaterPoint(nextPos, 0.3f))
							Velocity = Vector3.Zero;
					}
					MoveAndSlide();
					MaintainMovementLayer();
				}
				else
				{
					// 处于理想交火圈内：底盘平滑滑行减速入位并保持稳固射击姿态，炮塔持续独立瞄准开火
					// 彻底消除无意义的原地绕圈、甩尾打转与转弯晃动
					Velocity = Velocity.MoveToward(Vector3.Zero, MoveSpeed * 2.5f * delta);
					MoveAndSlide();
					MaintainMovementLayer();
				}
			}
			else
			{
				// 明确下达了原地驻守 (Hold Position) 指令：底盘平滑停稳
				Velocity = Velocity.MoveToward(Vector3.Zero, MoveSpeed * 4.0f * delta);
				MoveAndSlide();
				MaintainMovementLayer();
			}
		}
		else if (!SupportsArtilleryDeploy && dist < minSafetyDist && AttackRange >= 7.0f)
		{
			if (isInfantry)
			{
				// 1. 步兵贴脸突进：后撤拉开距离，边退边打
				var retreatDir = -toTarget.Normalized();
				Velocity = retreatDir * (MoveSpeed * 0.70f);
				ApplyFlankSeparationSteering();
				MoveAndSlide();
				MaintainMovementLayer();
				if (toTarget.Length() > 0.01f)
					FaceDirection(toTarget, delta);
				UpdateWeaponAim(targetPos, false, delta);
			}
			else
			{
				// 1. 战车/主战坦克贴脸：车头始终正对威胁（首上防御），沿履带轴向纯倒车后退，绝不侧滑
				var chassisForward = -GlobalTransform.Basis.Z;
				chassisForward.Y = 0f;
				if (chassisForward.LengthSquared() > 0.0001f)
					chassisForward = chassisForward.Normalized();
				else
					chassisForward = toTarget.Normalized();

				Velocity = -chassisForward * (MoveSpeed * 0.55f);
				MoveAndSlide();
				MaintainMovementLayer();
				if (toTarget.Length() > 0.01f)
					FaceDirection(toTarget, delta);
				UpdateWeaponAim(targetPos, false, delta);
			}
		}
		else if (isInfantry && !SupportsArtilleryDeploy && cooldownLeft > 0.35f && MoveSpeed >= 4.5f)
		{
			// 2. 武器装填间隙：仅轻装步兵可沿射程环小幅横移掩护（Skirmish Strafe），战车/坦克禁止任何横移侧滑
			var tangent = new Vector3(-toTarget.Z, 0f, toTarget.X).Normalized();
			float strafeSign = ((NetId % 2) == 0) ? 1.0f : -1.0f;
			Velocity = tangent * (strafeSign * MoveSpeed * 0.45f);
			ApplyFlankSeparationSteering();
			MoveAndSlide();
			MaintainMovementLayer();
			if (toTarget.Length() > 0.01f)
				FaceDirection(toTarget, delta);
			UpdateWeaponAim(targetPos, false, delta);
		}
		else
		{
			// 3. 稳固射击阵位开火：架设单位/步兵底盘停稳，平滑转正迎敌
			Velocity = Vector3.Zero;
			MoveAndSlide();
			MaintainMovementLayer();
			if (toTarget.Length() > 0.01f && ShouldBodyFaceAttackTarget())
				FaceDirection(toTarget, delta);
			UpdateWeaponAim(targetPos, false, delta);
		}

		if (!IsWeaponYawAligned(targetPos))
			return;

		float minRange = BattleUnitCatalog.MinAttackRange(UnitKey);
		if (minRange > 0f && dist < minRange)
			return;

		if (cooldownLeft <= 0f)
		{
			cooldownLeft = AttackCooldown;

			// 步兵与所有直射武器均为纯单体目标直射伤害，彻底杜绝自动投掷范围手榴弹；潜艇发射鱼雷，防空导弹车发射拦截导弹
			bool isTorpedoUnit = UnitKey == "submarine";
			bool isMissileUnit = UnitKey == "sam_missile";
			float distToTarget = AttackTarget is not null ? GlobalPosition.DistanceTo(AttackTarget.GlobalPosition) : 0f;

			PlayFireVisualAnimation(false);

			if (isTorpedoUnit && GodotObject.IsInstanceValid(AttackTarget))
			{
				CombatProjectile.SpawnTorpedo(this, this, AttackTarget, AttackDamage, PlayerOwned, AttackRange, SplashRadius);
			}
			else if (isMissileUnit && GodotObject.IsInstanceValid(AttackTarget))
			{
				CombatProjectile.SpawnMissile(this, this, AttackTarget, AttackDamage, PlayerOwned, AttackRange, SplashRadius);
			}
			else if (UnitKey == "aircraft_carrier")
			{
				// 航母近防自卫炮（CIWS 密集阵）：仅在敌方逼近 22 米近身防御圈时猛烈拦截射击，远程依靠舰载机
				if (distToTarget <= AttackRange && GodotObject.IsInstanceValid(AttackTarget))
				{
					CombatProjectile.Spawn(this, this, AttackTarget, AttackDamage, PlayerOwned, AttackRange, 0f, 0.5f);
				}
			}
			else if (GodotObject.IsInstanceValid(AttackTarget))
			{
				float dmg = AttackDamage;
				if (SupportsArtilleryDeploy && DeployState == ArtilleryDeployState.Deployed)
				{
					dmg *= BattleUnitCatalog.DeployedDamageMultiplier(UnitKey);
				}
				CombatProjectile.Spawn(this, this, AttackTarget, dmg, PlayerOwned, effectiveAttackRange, SplashRadius, SplashFalloff);
			}
		}
	}

	void ProcessAirCombat(float delta, Vector3 targetPos, Vector3 toTarget, float dist)
	{
		if (UnitKey == "bomber")
		{
			StartBombingRunOnPosition(targetPos);
			return;
		}

		bool isFixedWing = BattleUnitCatalog.IsFixedWingAircraft(UnitKey);
		var planeForward = -GlobalTransform.Basis.Z;
		planeForward.Y = 0f;
		if (planeForward.LengthSquared() > 0.0001f)
			planeForward = planeForward.Normalized();
		else
			planeForward = toTarget.LengthSquared() > 0.01f ? toTarget.Normalized() : Vector3.Forward;

		if (airCombatPhase == 0)
		{
			// 阶段 0：进击航线（高速俯冲接近、机头对准目标瞄准并开火）
			var targetDir = toTarget.LengthSquared() > 0.01f ? toTarget.Normalized() : planeForward;
			FaceDirection(targetDir, delta);

			float alignDot = planeForward.Dot(targetDir);
			float speedFactor = isFixedWing ? Mathf.Clamp(alignDot * 0.35f + 0.65f, 0.65f, 1.0f) : 1.0f;
			Velocity = (isFixedWing ? planeForward : targetDir) * (MoveSpeed * speedFactor);
			GlobalPosition += Velocity * delta;
			MaintainMovementLayer();

			// 处于有效射程内、装填完毕且机头对准目标（对准夹角小于 55 度）才实施攻击
			if (dist <= AttackRange && (!isFixedWing || alignDot >= 0.55f))
			{
				if (cooldownLeft <= 0f && GodotObject.IsInstanceValid(AttackTarget))
				{
					if (MaxAmmo > 0 && CurrentAmmo <= 0)
					{
						// 弹药耗尽，立即改出并返航母港补给装弹
						if (CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead)
						{
							AttackTarget = null;
							ReturnToCarrierBase();
							return;
						}
					}

					cooldownLeft = AttackCooldown;
					PlayFireVisualAnimation();

					if (MaxAmmo > 0)
					{
						CurrentAmmo = Mathf.Max(0, CurrentAmmo - 1);
					}

					if (UnitKey == "fighter")
					{
						CombatProjectile.SpawnMissile(this, this, AttackTarget, AttackDamage, PlayerOwned, AttackRange, SplashRadius);
					}
					else if (UnitKey == "bomber")
					{
						CombatProjectile.SpawnAerialBomb(this, this, AttackTarget.GlobalPosition, AttackDamage, PlayerOwned);
					}
					else
					{
						CombatProjectile.Spawn(this, this, AttackTarget, AttackDamage, PlayerOwned, AttackRange, 0f, 0.5f);
					}

					// 若最后一发弹药打空，立即进入改出回路并准备返航
					if (MaxAmmo > 0 && CurrentAmmo <= 0 && CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead)
					{
						var tangent = new Vector3(-planeForward.Z, 0f, planeForward.X).Normalized();
						float bankSide = (NetId % 2 == 0) ? 1.0f : -1.0f;
						airCombatEgressTarget = targetPos + planeForward * 30f + tangent * (bankSide * 16f);
						airCombatPhase = 1;
						airCombatTimer = 2.4f;
					}
				}
			}

			// 掠过目标头顶（距离小于 6.5 米，或已飞越至目标后方 alignDot <= 0.15）时，拉升大坡度改出建立战术间距
			if (dist <= Mathf.Max(6.5f, AttackRange * 0.35f) || (dist <= AttackRange && alignDot <= 0.15f))
			{
				var tangent = new Vector3(-planeForward.Z, 0f, planeForward.X).Normalized();
				float bankSide = (NetId % 2 == 0) ? 1.0f : -1.0f;
				airCombatEgressTarget = targetPos + planeForward * 32f + tangent * (bankSide * 18f);
				airCombatPhase = 1;
				airCombatTimer = 2.6f;
			}
		}
		else if (airCombatPhase == 1)
		{
			// 阶段 1：大坡度改出拉升（Breakaway & Extension，沿前向高速拉出 32 米安全战术间距）
			var egressDelta = airCombatEgressTarget - GlobalPosition;
			egressDelta.Y = 0f;
			var egressDir = egressDelta.LengthSquared() > 0.01f ? egressDelta.Normalized() : planeForward;
			FaceDirection(egressDir, delta);

			float alignDot = planeForward.Dot(egressDir);
			float speedFactor = isFixedWing ? Mathf.Clamp(alignDot * 0.35f + 0.65f, 0.65f, 1.0f) : 1.0f;
			Velocity = (isFixedWing ? planeForward : egressDir) * (MoveSpeed * speedFactor);
			GlobalPosition += Velocity * delta;
			MaintainMovementLayer();

			airCombatTimer -= delta;
			if (airCombatTimer <= 0f || egressDelta.LengthSquared() <= 25f)
			{
				if (CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead && ShouldReturnForCarrier())
				{
					AttackTarget = null;
					ReturnToCarrierBase();
					return;
				}

				// 改出拉开距离后，切入阶段 2：战术盘旋大回转回路 (Tactical Pylon Turnaround)
				airCombatPhase = 2;
				airCombatTimer = 3.8f;
			}
		}
		else
		{
			// 阶段 2：战术盘旋大回转（保持高速前向飞行，沿圆弧优雅回转对准目标，严禁原地打转）
			var targetDir = toTarget.LengthSquared() > 0.01f ? toTarget.Normalized() : planeForward;
			FaceDirection(targetDir, delta);

			float alignDot = planeForward.Dot(targetDir);
			Velocity = (isFixedWing ? planeForward : targetDir) * MoveSpeed;
			GlobalPosition += Velocity * delta;
			MaintainMovementLayer();

			airCombatTimer -= delta;
			// 当机头重新顺畅对准目标航向（夹角在 45 度以内），或者回转时间充足，重新切入阶段 0 进击航线
			if (alignDot >= 0.68f || airCombatTimer <= 0f)
			{
				airCombatPhase = 0;
			}
		}
	}

	void ApplyFlankSeparationSteering()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
			return;

		var myPos = GlobalPosition;
		Vector3 avoidance = Vector3.Zero;
		float myRadius = CollisionRadius;
		float checkRadius = BattleUnitCatalog.IsInfantryLike(UnitKey) ? (myRadius * 2.5f) : (myRadius * 2.0f + 1.2f);
		float checkRadiusSq = checkRadius * checkRadius;

		for (int i = 0; i < AllActiveUnits.Count; i++)
		{
			var other = AllActiveUnits[i];
			if (other == this || !GodotObject.IsInstanceValid(other) || other.IsDead || other.isDying)
				continue;
			if (BattleUnitCatalog.IsAirUnit(other.UnitKey))
				continue;

			var diff = myPos - other.GlobalPosition;
			diff.Y = 0f;
			float distSq = diff.LengthSquared();
			if (distSq > 0.01f && distSq < checkRadiusSq)
			{
				float dist = Mathf.Sqrt(distSq);
				var pushDir = diff / dist;
				var tangent = new Vector3(-pushDir.Z, 0f, pushDir.X);
				float weight = (checkRadius - dist) / checkRadius;
				avoidance += (pushDir * 0.6f + tangent * 0.5f) * weight;
			}
		}

		if (avoidance.LengthSquared() > 0.01f)
		{
			avoidance.Y = 0f;
			bool isVeh = !BattleUnitCatalog.IsNavalUnit(UnitKey) && !BattleUnitCatalog.IsInfantryLike(UnitKey) && !BattleUnitCatalog.IsAirUnit(UnitKey);
			if (isVeh)
			{
				// 装甲车辆严格禁止接受侧向横推力（杜绝履带侧滑），仅投影到车身前进/后退轴向
				var chassisForward = -GlobalTransform.Basis.Z;
				chassisForward.Y = 0f;
				if (chassisForward.LengthSquared() > 0.0001f)
				{
					chassisForward = chassisForward.Normalized();
					float forwardPush = avoidance.Dot(chassisForward);
					Velocity += chassisForward * (forwardPush * MoveSpeed * 0.15f);
				}
			}
			else
			{
				// 仅向步兵/常规非履带单位添加微弱侧向推力
				Velocity += avoidance.Normalized() * (MoveSpeed * 0.20f);
			}
		}
	}

	float GetForwardObstacleTrafficFactor(Vector3 forward)
	{
		float myRadius = CollisionRadius;
		float checkDist = myRadius * 2.2f + 1.2f;
		float minFactor = 1.0f;
		var myPos = GlobalPosition;

		for (int i = 0; i < AllActiveUnits.Count; i++)
		{
			var other = AllActiveUnits[i];
			if (other == this || !GodotObject.IsInstanceValid(other) || other.IsDead || other.isDying)
				continue;
			if (BattleUnitCatalog.IsAirUnit(other.UnitKey))
				continue;

			var diff = other.GlobalPosition - myPos;
			diff.Y = 0f;
			float d = diff.Length();
			if (d < 0.001f || d > checkDist)
				continue;

			float dot = forward.Dot(diff / d);
			if (dot > 0.65f) // 前方 49° 视锥范围障碍物
			{
				float otherRadius = other.CollisionRadius;
				float stopDist = myRadius + otherRadius + 0.35f;
				if (d <= stopDist)
				{
					return 0f; // 已抵达或小于安全停止距离，完全刹停排队，严禁顶撞前车
				}
				float factor = Mathf.Clamp((d - stopDist) / (checkDist - stopDist), 0f, 1f);
				if (factor < minFactor)
					minFactor = factor;
			}
		}

		return minFactor;
	}

	/// <summary>
	/// RTS 刚体硬解穿物理防重叠挤开系统 (Zero-GC PBD Circle-Circle Depenetration Solver)
	/// 每一个物理模拟帧执行，数学上 100% 杜绝任何两辆战车或单位重叠穿模。
	/// </summary>
	void ResolveLocalSeparation(float dt)
	{
		if (isDying || IsDead || BattleUnitCatalog.IsAirUnit(UnitKey))
			return;

		var myPos = GlobalPosition;
		float myRadius = CollisionRadius;
		float myMass = UnitMass;
		bool isNaval = BattleUnitCatalog.IsNavalUnit(UnitKey);
		bool myIsStationary = Velocity.LengthSquared() < 0.05f;
		float effectiveMyMass = myIsStationary ? myMass * 3.0f : myMass;

		for (int i = 0; i < AllActiveUnits.Count; i++)
		{
			var other = AllActiveUnits[i];
			if (other == this || !GodotObject.IsInstanceValid(other) || other.IsDead || other.isDying)
				continue;
			if (BattleUnitCatalog.IsAirUnit(other.UnitKey))
				continue;
			if (isNaval != BattleUnitCatalog.IsNavalUnit(other.UnitKey))
				continue;

			var otherPos = other.GlobalPosition;
			var diff = myPos - otherPos;
			diff.Y = 0f;
			float distSq = diff.LengthSquared();
			float otherRadius = other.CollisionRadius;
			float totalRadius = myRadius + otherRadius;
			float totalRadiusSq = totalRadius * totalRadius;

			if (distSq < totalRadiusSq)
			{
				float dist = Mathf.Sqrt(distSq);
				Vector3 pushDir;
				if (dist > 0.001f)
				{
					pushDir = diff / dist;
				}
				else
				{
					pushDir = -GlobalTransform.Basis.Z.Cross(Vector3.Up).Normalized();
					if (pushDir.LengthSquared() < 0.001f)
						pushDir = Vector3.Right;
				}

				float overlap = totalRadius - dist;
				bool otherIsStationary = other.Velocity.LengthSquared() < 0.05f;
				float effectiveOtherMass = otherIsStationary ? other.UnitMass * 3.0f : other.UnitMass;
				float totalMass = effectiveMyMass + effectiveOtherMass;
				float myShare = effectiveOtherMass / totalMass; // 重型/静止单位位移占比小
				float otherShare = 1f - myShare;

				// 1. 刚体硬解穿 (Hard Depenetration Displacement): 瞬间纠正穿透坐标，数学上彻底杜绝车体穿模
				Vector3 myPush = pushDir * (overlap * myShare);
				Vector3 otherPush = -pushDir * (overlap * otherShare);

				GlobalPosition += myPush;
				other.GlobalPosition += otherPush;

				// 2. 动量阻尼 (Velocity Deflection): 消除相对挤压速度分量，防止回弹震颤
				float myVIn = Velocity.Dot(-pushDir);
				if (myVIn > 0f)
				{
					Velocity -= (-pushDir) * myVIn;
				}
				float otherVIn = other.Velocity.Dot(pushDir);
				if (otherVIn > 0f)
				{
					other.Velocity -= pushDir * otherVIn;
				}
			}
		}

		// 3. 建筑障碍物物理硬阻挡 (Building Footprint Hard Clamping)
		if (BattleGameManager.Instance is { } mgr)
		{
			foreach (var building in mgr.GetBuildings())
			{
				if (!GodotObject.IsInstanceValid(building) || building.Health <= 0f)
					continue;
				var bPos = building.GlobalPosition;
				var bSize = building.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.Shape is BoxShape3D box ? box.Size : new Vector3(4f, 2f, 4f);
				float halfW = bSize.X * 0.5f + myRadius;
				float halfD = bSize.Z * 0.5f + myRadius;
				float dx = GlobalPosition.X - bPos.X;
				float dz = GlobalPosition.Z - bPos.Z;
				if (Mathf.Abs(dx) < halfW && Mathf.Abs(dz) < halfD)
				{
					float penX = halfW - Mathf.Abs(dx);
					float penZ = halfD - Mathf.Abs(dz);
					if (penX < penZ)
					{
						GlobalPosition = new Vector3(GlobalPosition.X + (dx >= 0 ? penX : -penX), GlobalPosition.Y, GlobalPosition.Z);
					}
					else
					{
						GlobalPosition = new Vector3(GlobalPosition.X, GlobalPosition.Y, GlobalPosition.Z + (dz >= 0 ? penZ : -penZ));
					}
				}
			}
		}
	}

	void ProcessAttackGround(float delta)
	{
		if (!CanAttackGroundPoint)
		{
			AttackGrounding = false;
			return;
		}

		var toTarget = attackGroundTarget - GlobalPosition;
		toTarget.Y = 0f;
		float effectiveAttackRange = (SupportsArtilleryDeploy && DeployState == ArtilleryDeployState.Deployed)
			? AttackRange * BattleUnitCatalog.DeployedRangeMultiplier(UnitKey)
			: AttackRange;

		if (toTarget.Length() > effectiveAttackRange)
		{
			if (SupportsArtilleryDeploy && (DeployState == ArtilleryDeployState.Deployed || DeployState == ArtilleryDeployState.Deploying))
			{
				BeginArtilleryUndeploy();
				return;
			}
			TargetPosition = NormalizeCommandPosition(attackGroundTarget);
			ProcessMove(delta);
			return;
		}

		if (SupportsArtilleryDeploy)
		{
			if (DeployState == ArtilleryDeployState.Mobile)
			{
				Velocity = Vector3.Zero;
				MoveAndSlide();
				BeginArtilleryDeploy();
				return;
			}
			if (DeployState == ArtilleryDeployState.Deploying)
			{
				Velocity = Vector3.Zero;
				MoveAndSlide();
				return;
			}
			if (DeployState == ArtilleryDeployState.Undeploying)
			{
				BeginArtilleryDeploy();
				Velocity = Vector3.Zero;
				MoveAndSlide();
				return;
			}
		}

		Velocity = Vector3.Zero;
		MoveAndSlide();
		if (toTarget.LengthSquared() > 0.0001f && ShouldBodyFaceAttackTarget())
			FaceDirection(toTarget, delta);
		UpdateWeaponAim(attackGroundTarget, true, delta);
		if (!IsWeaponYawAligned(attackGroundTarget))
			return;

		float minGroundRange = BattleUnitCatalog.MinAttackRange(UnitKey);
		if (minGroundRange > 0f && toTarget.Length() < minGroundRange)
			return;

		if (cooldownLeft <= 0f)
		{
			cooldownLeft = AttackCooldown;
			PlayFireVisualAnimation();
			float dmg = AttackDamage;
			if (SupportsArtilleryDeploy && DeployState == ArtilleryDeployState.Deployed)
			{
				dmg *= BattleUnitCatalog.DeployedDamageMultiplier(UnitKey);
			}
			CombatProjectile.SpawnGround(this, this, attackGroundTarget, dmg, PlayerOwned, effectiveAttackRange, SplashRadius, SplashFalloff);
		}
	}

	Vector3 NormalizeCommandPosition(Vector3 worldPos)
	{
		if (BattleGameManager.Instance is { } manager)
			return manager.NormalizeUnitTarget(this, worldPos);
		return BattleMapCatalog.ClampToMap(worldPos, 4f);
	}

	static Vector3 NormalizeAttackGroundTarget(Vector3 worldPos)
	{
		var map = BattleGameManager.Instance?.CurrentMap;
		if (BattleGameManager.Instance is { } manager)
		{
			var target = manager.ClampToPlayableMap(worldPos);
			float gy = BattleMapCatalog.GetTerrainHeightAt(map, target.X, target.Z, includeBridges: true);
			return new Vector3(target.X, gy, target.Z);
		}

		var clamped = BattleMapCatalog.ClampToMap(worldPos, 4f);
		float groundY = BattleMapCatalog.GetTerrainHeightAt(map, clamped.X, clamped.Z, includeBridges: true);
		return new Vector3(clamped.X, groundY, clamped.Z);
	}

	void MaintainMovementLayer()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			if (parkedAtAirfield)
			{
				GlobalPosition = new Vector3(GlobalPosition.X, AirfieldParkingHeight, GlobalPosition.Z);
			}
			else
			{
				var baseHeight = CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
				var map = BattleGameManager.Instance?.CurrentMap;
				float groundY = BattleMapCatalog.GetTerrainHeightAt(map, GlobalPosition.X, GlobalPosition.Z, includeBridges: true);
				float targetY = Mathf.Max(baseHeight, groundY + baseHeight * 0.75f);
				float newY = Mathf.MoveToward(GlobalPosition.Y, targetY, (float)GetPhysicsProcessDeltaTime() * 12.0f);
				GlobalPosition = new Vector3(GlobalPosition.X, newY, GlobalPosition.Z);
			}
		}
		else if (BattleUnitCatalog.IsNavalUnit(UnitKey))
		{
			UpdateNavalWaveBuoyancy();
		}
		else
		{
			var map = BattleGameManager.Instance?.CurrentMap;
			float targetGroundY = BattleMapCatalog.GetTerrainHeightAt(map, GlobalPosition.X, GlobalPosition.Z, includeBridges: true);
			float newY = Mathf.MoveToward(GlobalPosition.Y, targetGroundY, (float)GetPhysicsProcessDeltaTime() * 14.0f);
			GlobalPosition = new Vector3(GlobalPosition.X, newY, GlobalPosition.Z);
		}
	}

	void UpdateNavalWaveBuoyancy()
	{
		float t = (float)(Time.GetTicksMsec() / 1000.0);
		float seed = (float)(GetInstanceId() % 1000) * 0.173f;
		bool isSubmarine = UnitKey == "submarine";
		bool isMoving = Velocity.LengthSquared() > 0.05f;

		if (isSubmarine)
		{
			// 潜艇完全下潜节点深度为 Y = -2.53m；上浮为半潜航状态 (Y = -0.45m)
			float subHeave = Mathf.Sin(t * 0.85f + seed) * 0.02f + Mathf.Cos(t * 1.35f + seed * 1.2f) * 0.008f;
			float subPitch = Mathf.Sin(t * 0.75f + seed * 1.1f) * 0.010f;
			float subRoll = Mathf.Cos(t * 0.60f + seed * 0.8f) * 0.012f;

			// Y 代表节点基准高度：下潜目标深度精确为 Y = -2.53m
			float targetY = GlobalPosition.Y;
			if (targetY > -0.1f)
			{
				targetY = -0.45f; // 上浮默认半潜状态
			}
			else if (targetY <= -2.0f)
			{
				targetY = -2.53f; // 完全下潜 2.53 米深度
			}
			GlobalPosition = new Vector3(GlobalPosition.X, targetY + subHeave, GlobalPosition.Z);
			if (GetNodeOrNull("SubmarineVisual") is Node3D subVisual)
			{
				subVisual.Rotation = new Vector3(subPitch, 0f, subRoll);
				ApplySubmarineUnderwaterRenderPriority(subVisual);
			}
			return;
		}

	static void ApplySubmarineUnderwaterRenderPriority(Node3D visual)
	{
		if (visual.HasMeta("SubmarineRenderPrioritySet")) return;
		visual.SetMeta("SubmarineRenderPrioritySet", true);

		foreach (var child in visual.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (child is MeshInstance3D mesh)
			{
				mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
			}
		}
	}

		// 航空母舰 (10万吨级超级核动力航母)：拥有巨大的惯性质量与双减摇鳍稳定系统，
		// 航行与移动时为舰载机起降提供绝对平稳刚体飞行甲板，消除任何波浪抖动与杠杆力臂晃动
		if (UnitKey == "aircraft_carrier")
		{
			GlobalPosition = new Vector3(GlobalPosition.X, BattleUnitCatalog.SpawnHeight(UnitKey), GlobalPosition.Z);
			if (GetNodeOrNull("AircraftCarrier") is Node3D acVisual)
			{
				acVisual.Rotation = Vector3.Zero;
			}
			else if (GetNodeOrNull("CarrierVisual") is Node3D cvVisual)
			{
				cvVisual.Rotation = Vector3.Zero;
			}
			return;
		}

		// 其他水面舰艇 (战列舰/驱逐舰/巡逻艇/运输船) 破浪沉浮与倾斜
		float speedMult = isMoving ? 1.25f : 1.0f;
		float heaveFreq = 1.2f;
		float heaveAmp = UnitKey == "battleship" ? 0.05f : 0.12f;
		float heave = Mathf.Sin(t * heaveFreq * speedMult + seed) * heaveAmp +
					  Mathf.Cos(t * heaveFreq * 1.5f * speedMult + seed * 1.3f) * (heaveAmp * 0.35f);

		float pitchAmp = isMoving ? 0.035f : 0.025f;
		float pitch = Mathf.Sin(t * 1.1f * speedMult + seed * 1.1f) * pitchAmp;
		if (isMoving)
			pitch -= 0.015f; // 航行时船头微扬

		float rollAmp = isMoving ? 0.045f : 0.030f;
		float roll = Mathf.Cos(t * 0.85f * speedMult + seed * 0.8f) * rollAmp;

		GlobalPosition = new Vector3(GlobalPosition.X, BattleUnitCatalog.SpawnHeight(UnitKey) + heave, GlobalPosition.Z);

		if (GetNodeOrNull("BattleshipVisual") is Node3D bsVisual)
		{
			bsVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
		else if (GetNodeOrNull("AircraftCarrier") is Node3D acVisual)
		{
			acVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
		else if (GetNodeOrNull("NimitzCarrier") is Node3D carrierVisual)
		{
			carrierVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
		else if (GetNodeOrNull("DestroyerVisual") is Node3D desVisual)
		{
			desVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
		else if (GetNodeOrNull("PatrolBoatVisual") is Node3D pbVisual)
		{
			pbVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
		else if (GetNodeOrNull("TransportVisual") is Node3D transVisual)
		{
			transVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
		else if (GetNodeOrNull("ImportedShip") is Node3D importedVisual)
		{
			importedVisual.Rotation = new Vector3(pitch, 0f, roll);
		}
	}

	void ProcessBombingRun(float delta)
	{
		var flatSelf = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
		var planeForward = -GlobalTransform.Basis.Z;
		planeForward.Y = 0f;
		if (planeForward.LengthSquared() > 0.0001f)
			planeForward = planeForward.Normalized();
		else
			planeForward = bombingRunDirection;

		if (!bombingRunOnAttackLeg)
		{
			// 进场阶段：转向并全速飞向投弹进场点 bombingRunApproach
			var toApproach = bombingRunApproach - GlobalPosition;
			toApproach.Y = 0f;
			var approachDir = toApproach.LengthSquared() > 0.01f ? toApproach.Normalized() : bombingRunDirection;
			FaceDirection(approachDir, delta);

			float align = planeForward.Dot(approachDir);
			float speedFactor = Mathf.Clamp(align * 0.35f + 0.65f, 0.65f, 1.0f);
			Velocity = planeForward * (MoveSpeed * speedFactor);
			GlobalPosition += Velocity * delta;
			MaintainMovementLayer();

			var flatApproach = new Vector3(bombingRunApproach.X, 0f, bombingRunApproach.Z);
			// 距离进场点小于 5.5 米，或者机体已经沿轰炸方向越过了进场点截面，直接切入投弹航线
			if (flatSelf.DistanceTo(flatApproach) <= 5.5f || (flatSelf - flatApproach).Dot(bombingRunDirection) >= 0f)
			{
				bombingRunOnAttackLeg = true;
			}
			return;
		}

		// 投弹冲刺阶段：严格对准航向 bombingRunDirection 全速直线掠空飞越
		FaceDirection(bombingRunDirection, delta);
		Velocity = planeForward * MoveSpeed;
		GlobalPosition += Velocity * delta;
		MaintainMovementLayer();

		// 判定是否达到投弹起点（达到投弹距离即可触发连续投弹）
		var runOffset = flatSelf - new Vector3(bombingRunStart.X, 0f, bombingRunStart.Z);
		var runProgress = runOffset.Dot(bombingRunDirection);
		if (!bombingRunVolleyReleased && runProgress + 0.5f >= bombingRunReleaseDistance)
		{
			ReleaseBombingRunVolley();
		}

		// 判定是否完成本次空袭脱离航线
		var flatExit = new Vector3(bombingRunExit.X, 0f, bombingRunExit.Z);
		var exitOffset = flatSelf - flatExit;
		if (exitOffset.Dot(bombingRunDirection) >= 0f || flatSelf.DistanceTo(flatExit) <= 4.0f)
		{
			CancelBombingRun();
			if (CarrierHome is not null && GodotObject.IsInstanceValid(CarrierHome) && !CarrierHome.IsDead && ShouldReturnForCarrier())
			{
				ReturnToCarrierBase();
			}
			else
			{
				// 脱离后切入高空大半径盘旋待机回路 (Loiter Orbit)
				StartAirLoiterOrbit(GlobalPosition + planeForward * 15f);
			}
		}
	}

	void AcquireAttackMoveTarget(float delta)
	{
		acquireTimer -= delta;
		if (acquireTimer > 0f || BattleGameManager.Instance is not { } manager)
			return;

		acquireTimer = 0.35f;
		float searchRange = UnitKey == "aircraft_carrier" ? BattleUnitCatalog.CarrierAirStrikeRange : AttackRange;
		var target = FindBestTarget(manager, searchRange);
		if (target is not null)
			AttackTarget = target;
	}

	void AcquireGuardTarget(float delta)
	{
		acquireTimer -= delta;
		if (acquireTimer > 0f || BattleGameManager.Instance is not { } manager)
			return;

		acquireTimer = 0.45f;
		float searchRange = UnitKey == "aircraft_carrier" ? BattleUnitCatalog.CarrierAirStrikeRange : AttackRange;
		AttackTarget = FindBestTarget(manager, searchRange);
	}

	void AcquireOpportunisticTarget(float delta)
	{
		acquireTimer -= delta;
		if (acquireTimer > 0f || BattleGameManager.Instance is not { } manager)
			return;

		acquireTimer = 0.30f;
		float searchRange = UnitKey == "aircraft_carrier" ? BattleUnitCatalog.CarrierAirStrikeRange : AttackRange;
		var target = FindBestTarget(manager, searchRange);
		if (target is not null)
		{
			AttackTarget = target;
		}
	}

	void ProcessPatrol(float delta)
	{
		if (BattleGameManager.Instance is not { } manager)
			return;

		acquireTimer -= delta;
		if (acquireTimer <= 0f)
		{
			acquireTimer = 0.35f;
			var target = FindBestTarget(manager, AttackRange * 1.45f);
			if (target is not null)
			{
				AttackTarget = target;
				return;
			}
		}

		if (IsIdle())
		{
			patrolHeadingToB = !patrolHeadingToB;
			TargetPosition = patrolHeadingToB ? patrolB : patrolA;
		}
	}

	void ProcessGuard(float delta)
	{
		if (guardTarget is null || !GodotObject.IsInstanceValid(guardTarget) || guardTarget.IsDead)
		{
			Stop();
			return;
		}

		if (BattleGameManager.Instance is not { } manager)
			return;

		acquireTimer -= delta;
		if (acquireTimer <= 0f)
		{
			acquireTimer = 0.35f;
			var target = FindBestTarget(manager, AttackRange * 1.8f);
			if (target is not null && target.GlobalPosition.DistanceTo(guardTarget.GlobalPosition) <= 22f)
			{
				AttackTarget = target;
				return;
			}
		}

		if (GlobalPosition.DistanceTo(guardTarget.GlobalPosition) > 6f || IsIdle())
			TargetPosition = NormalizeCommandPosition(GuardFollowPosition());
	}

	Vector3 GuardFollowPosition()
	{
		if (guardTarget is null || !GodotObject.IsInstanceValid(guardTarget))
			return GlobalPosition;

		var toSelf = GlobalPosition - guardTarget.GlobalPosition;
		toSelf.Y = 0f;
		var direction = toSelf.LengthSquared() > 0.1f ? toSelf.Normalized() : Vector3.Right;
		return guardTarget.GlobalPosition + direction * 4f;
	}

	Node3D? FindBestTarget(BattleGameManager manager, float maxRange)
	{
		Node3D? target = null;
		var maxDistanceSquared = maxRange * maxRange;
		var bestScore = float.NegativeInfinity;

		foreach (var unit in manager.GetUnits(!PlayerOwned))
		{
			if (!GodotObject.IsInstanceValid(unit) || unit.IsDead)
				continue;
			if (!BattleUnitCatalog.CanAttackTargetType(UnitKey, unit.UnitKey))
				continue;
			var distanceSquared = unit.GlobalPosition.DistanceSquaredTo(GlobalPosition);
			if (distanceSquared > maxDistanceSquared)
				continue;
			var score = TargetPriorityScore(unit, distanceSquared);
			if (score <= bestScore)
				continue;
			bestScore = score;
			target = unit;
		}

		foreach (var building in manager.GetBuildings(!PlayerOwned))
		{
			if (!GodotObject.IsInstanceValid(building) || building.Health <= 0f)
				continue;
			var distanceSquared = building.GlobalPosition.DistanceSquaredTo(GlobalPosition);
			if (distanceSquared > maxDistanceSquared)
				continue;
			var score = TargetPriorityScore(building, distanceSquared);
			if (score <= bestScore)
				continue;
			bestScore = score;
			target = building;
		}

		return target;
	}

	float TargetPriorityScore(Node3D target, float distanceSquared)
	{
		var score = 1000f - Mathf.Sqrt(distanceSquared) * 6f;
		if (target is RtsUnit unit)
		{
			score += 260f; // 优先攻击所有能反击的敌方移动单位

			var targetIsAir = BattleUnitCatalog.IsAirUnit(unit.UnitKey);
			var targetIsInfantry = BattleUnitCatalog.IsInfantryLike(unit.UnitKey);
			var targetIsNaval = BattleUnitCatalog.IsNavalUnit(unit.UnitKey);

			if (targetIsAir)
			{
				if (unit.UnitKey == "scout_plane")
					score += UnitKey == "anti_air_gun" ? 480f : 120f; // 低空侦察机进入陆军射程，地面装甲与机枪可优先截击
				else
					score += UnitKey is "anti_air_gun" or "fighter" ? 520f : -380f;
			}
			if (UnitKey is "infantry_flamethrower" or "flamethrower" && targetIsInfantry)
				score += 430f;
			if (BattleUnitCatalog.IsInfantryLike(UnitKey) && targetIsInfantry)
				score += 120f;
			if (BattleUnitCatalog.IsNavalUnit(UnitKey) && targetIsNaval)
				score += 300f;
			if (UnitKey == "fighter" && targetIsAir)
				score += 220f;
		}
		else if (target is RtsBuilding building)
		{
			score += -120f; // 大幅降低对非防御性建筑的默认攻击权重
			if (UnitKey is "artillery" or "bomber" or "destroyer_ship" or "transport_ship")
				score += 550f; // 攻城和轰炸单位依然优先轰炸建筑
			if (UnitKey is "anti_air_gun" or "fighter")
				score -= 360f;
			if (building.IsDefenseTurret)
				score += 280f; // 具有反击能力的炮塔，权重高一些
		}

		return score;
	}

	bool IsIdle()
	{
		var flatDelta = TargetPosition - GlobalPosition;
		flatDelta.Y = 0f;
		return flatDelta.LengthSquared() <= 0.05f;
	}

	bool ShouldBodyFaceAttackTarget()
		=> BattleUnitCatalog.IsAirUnit(UnitKey)
			|| BattleUnitCatalog.IsInfantryLike(UnitKey)
			|| BattleUnitCatalog.IsNavalUnit(UnitKey)
			|| UnitKey == "artillery"
			|| weaponYawNode is null;

	public void FaceDirectionImmediate(Vector3 direction)
	{
		direction.Y = 0f;
		if (direction.LengthSquared() <= 0.0001f)
			return;

		Rotation = new Vector3(Rotation.X, ResolveFacingYaw(direction), Rotation.Z);
	}

	public void DebugForceRotateTurret(float degrees)
	{
		if (weaponYawNode is not null && GodotObject.IsInstanceValid(weaponYawNode))
		{
			weaponYawNode.Rotation = new Vector3(weaponYawNode.Rotation.X, Mathf.DegToRad(degrees), weaponYawNode.Rotation.Z);
		}
	}

	float ResolveFacingYaw(Vector3 direction)
		=> Mathf.Atan2(-direction.X, -direction.Z) + bodyFacingYawOffset;

	void FaceDirection(Vector3 direction, float delta)
	{
		direction.Y = 0f;
		if (direction.LengthSquared() <= 0.0001f)
			return;

		var targetYaw = ResolveFacingYaw(direction);
		var currentYaw = Rotation.Y;
		float turnSharpness = BattleUnitCatalog.GetTurnSpeed(UnitKey);
		float deltaYaw = Mathf.Wrap(targetYaw - currentYaw, -Mathf.Pi, Mathf.Pi);

		// 平滑转向缓出阻尼 (Smooth Angular Deceleration & Damping)
		// 在接近目标朝向时平滑减速入轨，彻底消除到达临界角瞬间的硬停甩头与突兀抽搐
		float absDelta = Mathf.Abs(deltaYaw);
		float speedScale = Mathf.Clamp(absDelta / 0.35f, 0.15f, 1.0f); // 20度以内平滑缓速对齐
		float maxStep = turnSharpness * speedScale * delta;
		float step = Mathf.Clamp(deltaYaw, -maxStep, maxStep);
		Rotation = new Vector3(0f, currentYaw + step, 0f);
	}

	void UpdateWeaponAim(Vector3 target, bool groundAttack, float delta)
	{
		if (weaponYawNode is null && weaponPitchNode is null)
			return;

		if (!IsNodeReadyForWorldAim(weaponYawNode) || !IsNodeReadyForWorldAim(weaponPitchNode))
			return;

		var aimTarget = target;
		bool isAntiAir = UnitKey == "anti_air_gun" || UnitKey == "sam_missile";
		bool targetIsScoutPlane = AttackTarget is RtsUnit tu && (tu.UnitKey == "scout_plane" || tu.UnitKey == "scout_helicopter");
		if (isAntiAir || BattleUnitCatalog.IsAirUnit(UnitKey) || targetIsScoutPlane)
		{
			aimTarget.Y = target.Y;
		}
		else
		{
			aimTarget.Y = GlobalPosition.Y + (BattleUnitCatalog.IsArtilleryLike(visualKey) ? 2.3f : 0.65f);
		}

		if (weaponYawNode is not null)
		{
			var yawTarget = aimTarget;
			yawTarget.Y = weaponYawNode.GlobalPosition.Y;
			ApplySmoothLookAt(weaponYawNode, yawTarget, weaponYawLookOffset, delta, 8.5f);
		}

		if (weaponPitchNode is not null)
		{
			if (UnitKey == "artillery" || BattleUnitCatalog.IsArtilleryLike(visualKey))
			{
				// 榴弹炮真实大仰角间接瞄准射击 (High-angle indirect howitzer trajectory):
				// 近距离大仰角吊射 (~64°)，远距离中高仰角压制 (~46°)，静止架设待机仰角约 58°
				float dist = Mathf.Max(5f, (aimTarget - GlobalPosition).Length());
				float rangeFrac = Mathf.Clamp(dist / Mathf.Max(10f, AttackRange), 0f, 1f);
				float indirectElevation = Mathf.Lerp(Mathf.DegToRad(64f), Mathf.DegToRad(46f), rangeFrac);
				weaponPitchNode.Rotation = SmoothEuler(weaponPitchNode.Rotation, new Vector3(indirectElevation, 0f, 0f), delta, 6.0f);
			}
			else if (weaponPitchIsPivot)
			{
				var pitchTarget = aimTarget;
				if (groundAttack)
					pitchTarget.Y += Mathf.Clamp(AttackRange * 0.08f, 0.85f, 2.8f);
				var localTarget = weaponPitchNode.ToLocal(pitchTarget);
				var desired = new Vector3(Mathf.Atan2(localTarget.Y, -localTarget.Z), 0f, 0f) + weaponPitchPivotOffset;
				if (!isAntiAir && (visualKey is "tank" or "heavy_tank" or "light_tank" or "medium_tank"))
				{
					float maxPitchDeg = targetIsScoutPlane ? 32f : 16f;
					desired.X = Mathf.Clamp(desired.X, weaponPitchPivotOffset.X - Mathf.DegToRad(6f), weaponPitchPivotOffset.X + Mathf.DegToRad(maxPitchDeg));
				}
				weaponPitchNode.Rotation = SmoothEuler(weaponPitchNode.Rotation, desired, delta, 7.5f);
			}
			else
			{
				var pitchTarget = aimTarget;
				float minP = isAntiAir ? -10f : -6f;
				float maxP = isAntiAir ? 80f : (targetIsScoutPlane ? 32f : 16f);
				ApplySmoothLookAt(weaponPitchNode, pitchTarget, weaponPitchLookOffset, delta, 7.5f, minP, maxP);
			}
		}
	}

	bool IsWeaponYawAligned(Vector3 target)
	{
		// 处于搭炮架/收起炮架过渡期不可开火
		if (SupportsArtilleryDeploy && DeployState != ArtilleryDeployState.Deployed)
			return false;

		// 如果没有独立的武器旋转炮塔（如普通步兵或自行火炮），车身在射程内对准即可开火
		if (weaponYawNode is null)
		{
			if (UnitKey == "artillery")
			{
				// 自行火炮车身需大体迎敌 (Within 25 degrees)
				var toTargetUnit = target - GlobalPosition;
				toTargetUnit.Y = 0f;
				if (toTargetUnit.LengthSquared() > 0.0001f)
				{
					var fwd = -GlobalTransform.Basis.Z;
					fwd.Y = 0f;
					if (fwd.Normalized().Dot(toTargetUnit.Normalized()) < 0.90f)
						return false;
				}
			}
			else
			{
				return true;
			}
		}
		else
		{
			var yawNode = weaponYawNode;
			if (!IsNodeReadyForWorldAim(yawNode))
				return false;

			var toTarget = target - yawNode.GlobalPosition;
			toTarget.Y = 0f;
			if (toTarget.LengthSquared() <= 0.0001f)
				return true;

			var forward = ResolveAimForward(yawNode, weaponYawLookOffset);
			forward.Y = 0f;
			if (forward.LengthSquared() <= 0.0001f)
				return true;

			// 提升水平旋转对准精度：静止时对准约 10 度以内（dot >= 0.985f）；行进动态射击/装甲机动时放宽至约 18 度（dot >= 0.95f）
			float requiredYawDot = CanFireWhileMoving ? 0.95f : 0.985f;
			float yawDot = forward.Normalized().Dot(toTarget.Normalized());
			if (yawDot < requiredYawDot)
				return false;
		}

		// 校验炮管俯仰对准：若有独立俯仰节点，校验垂直方向夹角
		if (weaponPitchNode is not null && IsNodeReadyForWorldAim(weaponPitchNode))
		{
			if (UnitKey == "artillery" || BattleUnitCatalog.IsArtilleryLike(visualKey))
			{
				// 榴弹炮为大仰角高曲射武器：只要炮管立起达到曲射区间 (>= 40°，即 >= 0.70 rad) 即可允许击发
				float currentPitch = weaponPitchNode.Rotation.X;
				if (currentPitch < Mathf.DegToRad(40f))
					return false;
			}
			else
			{
				var pitchToTarget = (target - weaponPitchNode.GlobalPosition).Normalized();
				var pitchFwd = ResolveAimForward(weaponPitchNode, weaponPitchLookOffset).Normalized();
				if (weaponPitchIsPivot)
				{
					var localFwd = Basis.FromEuler(-weaponPitchPivotOffset) * new Vector3(0f, 0f, -1f);
					pitchFwd = (weaponPitchNode.GlobalTransform.Basis * localFwd).Normalized();
				}
				float requiredPitchDot = CanFireWhileMoving ? 0.91f : 0.95f;
				if (pitchFwd.Dot(pitchToTarget) < requiredPitchDot)
					return false;
			}
		}

		return true;
	}

	static Vector3 ResolveAimForward(Node3D node, Vector3 lookOffset)
	{
		var localForward = Basis.FromEuler(lookOffset) * new Vector3(0f, 0f, -1f);
		return node.GlobalTransform.Basis * localForward;
	}

	static void ApplySmoothLookAt(Node3D node, Vector3 target, Vector3 offset, float delta, float speed, float minPitchDeg = -14f, float maxPitchDeg = 32f)
	{
		if (!IsNodeReadyForWorldAim(node))
			return;

		var previous = node.GlobalTransform;
		node.LookAt(target, Vector3.Up, false);
		var desiredRotation = node.Rotation + offset;
		desiredRotation.X = Mathf.Clamp(desiredRotation.X, Mathf.DegToRad(minPitchDeg), Mathf.DegToRad(maxPitchDeg));
		node.GlobalTransform = previous;
		node.Rotation = SmoothEuler(node.Rotation, desiredRotation, delta, speed);
	}

	static bool IsNodeReadyForWorldAim(Node3D? node)
		=> node is not null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();

	static Vector3 SmoothEuler(Vector3 current, Vector3 desired, float delta, float speed)
	{
		var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * delta);
		return new Vector3(
			Mathf.LerpAngle(current.X, desired.X, t),
			Mathf.LerpAngle(current.Y, desired.Y, t),
			Mathf.LerpAngle(current.Z, desired.Z, t));
	}

	void ResolveVisualAnimationNames()
	{
		if (visualAnimationPlayers.Count == 0)
			return;

		foreach (var player in visualAnimationPlayers)
		{
			// 为所有动画播放器设置默认融合时间为 0.2 秒，使 idle, walk, run, fire 状态平滑过渡，消除生硬的关节跳转
			player.PlaybackDefaultBlendTime = 0.2f;
			foreach (var animation in player.GetAnimationList())
			{
				var name = animation.ToString();
				var lower = name.ToLowerInvariant();

				if (lower.Contains("walk") && string.IsNullOrEmpty(walkAnimationName))
					walkAnimationName = name;
				else if (lower.Contains("run") && string.IsNullOrEmpty(runAnimationName))
					runAnimationName = name;
				else if (string.IsNullOrEmpty(walkAnimationName) && lower.Contains("move"))
					walkAnimationName = name;

				if (string.IsNullOrEmpty(idleAnimationName) && lower.Contains("idle"))
					idleAnimationName = name;
				if (string.IsNullOrEmpty(fireAnimationName)
					&& (lower.Contains("fire") || lower.Contains("shoot") || lower.Contains("attack")))
					fireAnimationName = name;
				if (string.IsNullOrEmpty(hitAnimationName)
					&& (lower.Contains("hit") || lower.Contains("react") || lower.Contains("damage")))
					hitAnimationName = name;
				if (string.IsNullOrEmpty(reloadAnimationName) && lower.Contains("reload"))
					reloadAnimationName = name;
				if (string.IsNullOrEmpty(grenadeAnimationName) && (lower.Contains("grenade") || lower.Contains("toss")))
					grenadeAnimationName = name;
			}
		}

		// 回退互保：若无独立奔跑动画则以行走充当，若无行走动画则以奔跑充当
		if (string.IsNullOrEmpty(walkAnimationName))
			walkAnimationName = runAnimationName;
		if (string.IsNullOrEmpty(runAnimationName))
			runAnimationName = walkAnimationName;
	}

	void CaptureInfantryVisualParts(Node3D root)
	{
		if (!BattleUnitCatalog.IsInfantryLike(visualKey))
			return;

		// 如果模型拥有标准的骨骼动画播放器，就不应捕获骨骼进行手脚摆动的纯逻辑二次覆盖，避免冲突导致频繁抖动/闪烁
		if (visualAnimationPlayers.Count > 0)
			return;

		var allNodes = root.FindChildren("*", "Node3D", true, false)
			.OfType<Node3D>()
			.ToArray();
		var bodyNodes = allNodes
			.Where(node => IsInfantryBodyNode(node, root))
			.Where(node => !HasInfantryBodyAncestor(node, root))
			.ToList();
		if (bodyNodes.Count == 0)
			bodyNodes.Add(root);

		for (var i = 0; i < bodyNodes.Count; i++)
			AddAnimatedInfantryPart(bodyNodes[i], InfantryVisualPartKind.Body, i * 1.7f);

		foreach (var node in allNodes)
		{
			if (!TryResolveInfantryPartKind(node.Name.ToString(), out var kind))
				continue;
			var bodyIndex = FindInfantryBodyIndex(node, bodyNodes);
			AddAnimatedInfantryPart(node, kind, bodyIndex * 1.7f);
		}
	}

	void AddAnimatedInfantryPart(Node3D node, InfantryVisualPartKind kind, float phase)
	{
		if (infantryVisualParts.Any(part => part.Node == node))
			return;

		infantryVisualParts.Add(new AnimatedVisualPart
		{
			Node = node,
			RestPosition = node.Position,
			RestRotation = node.Rotation,
			Phase = phase,
			Kind = kind
		});
	}

	static bool IsInfantryBodyNode(Node3D node, Node3D root)
	{
		if (node == root)
			return false;

		var lower = node.Name.ToString().ToLowerInvariant();
		return !lower.Contains("squad")
			&& (lower.StartsWith("infantry_")
				|| lower.StartsWith("soldier")
				|| lower.StartsWith("rifleman")
				|| lower.StartsWith("trooper"));
	}

	static bool HasInfantryBodyAncestor(Node3D node, Node3D root)
	{
		var parent = node.GetParent();
		while (parent is Node3D parent3D && parent3D != root)
		{
			if (IsInfantryBodyNode(parent3D, root))
				return true;
			parent = parent.GetParent();
		}
		return false;
	}

	static bool TryResolveInfantryPartKind(string name, out InfantryVisualPartKind kind)
	{
		var lower = name.ToLowerInvariant().Replace("_", "").Replace("-", "");
		if (lower.Contains("strideleftleg") || lower.Contains("leftleg"))
		{
			kind = InfantryVisualPartKind.LeftLeg;
			return true;
		}

		if (lower.Contains("striderightleg") || lower.Contains("rightleg"))
		{
			kind = InfantryVisualPartKind.RightLeg;
			return true;
		}

		if (lower.Contains("strideleftarm") || lower.Contains("leftarm"))
		{
			kind = InfantryVisualPartKind.LeftArm;
			return true;
		}

		if (lower.Contains("striderightarm") || lower.Contains("rightarm"))
		{
			kind = InfantryVisualPartKind.RightArm;
			return true;
		}

		kind = InfantryVisualPartKind.Body;
		return false;
	}

	static int FindInfantryBodyIndex(Node3D node, IReadOnlyList<Node3D> bodyNodes)
	{
		var parent = node.GetParent();
		while (parent is Node3D parent3D)
		{
			for (var i = 0; i < bodyNodes.Count; i++)
			{
				if (bodyNodes[i] == parent3D)
					return i;
			}
			parent = parent.GetParent();
		}
		return Mathf.Max(0, bodyNodes.Count - 1);
	}

	void CapturePropellerNodes(Node3D root)
	{
		if (!BattleUnitCatalog.IsAirUnit(visualKey) && visualKey != "submarine" && !BattleUnitCatalog.IsNavalUnit(visualKey))
			return;

		// 直升机拥有专用的骨骼动画与高动态程序化尾旋翼转子系统，避免通用的机鼻/舰船螺旋桨粗暴绕 Z 轴旋转污染
		if (visualKey == "scout_plane" || UnitKey == "scout_plane" || visualKey == "scout_helicopter" || UnitKey == "scout_helicopter")
			return;

		if (visualKey == "submarine" || UnitKey == "submarine")
		{
			var propRig = root.FindChild("SubmarinePropellerRig", true, false) as Node3D;
			if (propRig == null)
			{
				var firstBlade = root.FindChildren("Plane_00*", "MeshInstance3D", true, false)
					.OfType<MeshInstance3D>()
					.FirstOrDefault(m => m.Position.Z > 4.5f);

				if (firstBlade != null && firstBlade.GetParent() is Node3D parentNode)
				{
					var blades = parentNode.FindChildren("Plane_00*", "MeshInstance3D", false, false)
						.OfType<MeshInstance3D>()
						.ToList();
					var hub = parentNode.FindChild("Cylinder_001", false, false) as MeshInstance3D;

					if (blades.Count > 0)
					{
						var pivotPos = hub != null ? hub.Position : Vector3.Zero;
						if (pivotPos == Vector3.Zero)
						{
							foreach (var b in blades)
								pivotPos += b.Position;
							pivotPos /= blades.Count;
						}

						propRig = new Node3D
						{
							Name = "SubmarinePropellerRig",
							Position = pivotPos
						};
						parentNode.AddChild(propRig);

						foreach (var blade in blades)
						{
							var oldPos = blade.Position;
							blade.Reparent(propRig, false);
							blade.Position = oldPos - propRig.Position;
						}
						if (hub != null)
						{
							var oldPos = hub.Position;
							hub.Reparent(propRig, false);
							hub.Position = oldPos - propRig.Position;
						}
					}
				}
			}

			if (propRig != null && !propellerNodes.Contains(propRig))
			{
				propellerNodes.Add(propRig);
				return;
			}

			var customProp = root.FindChild("SubmarinePropeller", true, false) as Node3D;
			if (customProp != null && !propellerNodes.Contains(customProp))
			{
				propellerNodes.Add(customProp);
				return;
			}
		}

		var allNodes = root.FindChildren("*", "Node3D", true, false)
			.OfType<Node3D>()
			.ToArray();
		var primaryNodes = allNodes.Where(IsPrimaryPropellerNode).ToArray();
		foreach (var node in primaryNodes)
		{
			if (!HasCapturedPropellerAncestor(node))
				propellerNodes.Add(node);
		}

		if (propellerNodes.Count > 0)
			return;

		foreach (var node in allNodes.Where(IsFallbackPropellerNode))
		{
			if (!HasCapturedPropellerAncestor(node))
				propellerNodes.Add(node);
		}
	}

	bool HasCapturedPropellerAncestor(Node3D node)
	{
		var parent = node.GetParent();
		while (parent is Node3D parent3D)
		{
			if (propellerNodes.Contains(parent3D))
				return true;
			parent = parent.GetParent();
		}
		return false;
	}

	static bool IsPrimaryPropellerNode(Node3D node)
	{
		var lower = node.Name.ToString().ToLowerInvariant();
		return !lower.Contains("blade")
			&& !lower.Contains("hub")
			&& !lower.Contains("blur")
			&& !lower.Contains("disc")
			&& !lower.Contains("attachment")
			&& !lower.Contains("rig")
			&& (lower.Contains("propeller")
				|| lower.Contains("rotor")
				|| lower == "prop"
				|| lower.StartsWith("prop_")
				|| lower.StartsWith("prop-"));
	}

	static bool IsFallbackPropellerNode(Node3D node)
	{
		var lower = node.Name.ToString().ToLowerInvariant();
		return !lower.Contains("blur")
			&& !lower.Contains("disc")
			&& !lower.Contains("attachment")
			&& !lower.Contains("rig")
			&& (lower.Contains("propeller")
				|| lower.Contains("prop")
				|| lower.Contains("rotor")
				|| lower.Contains("blade"));
	}

	void UpdateVisualState(float delta)
	{
		var actuallyMoving = Velocity.LengthSquared() > 0.08f || bombingRunActive || parkingAtAirfield;
		if (actuallyMoving)
		{
			stationaryDuration = 0f;
		}
		else
		{
			stationaryDuration += delta;
		}

		// 采用非对称移动状态过滤（滞后滤波器）：起步瞬时判定为走，但停止动画延迟 0.22 秒触发。
		// 这避免了单位在寻路、绕路时因单帧速度骤减为 0 而频繁在 walk/idle 间来回闪烁闪现。
		var moving = actuallyMoving || (stationaryDuration < 0.22f);
		UpdateAnimationPlayback(moving, delta);
		UpdateInfantryStride(moving);
		UpdatePropellers(delta, moving);

		// 载具与火炮开火机械后坐力弹簧阻尼平滑复位 (Spring-back damper)
		if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode) && hasRecordedWeaponPitchRest)
		{
			if (weaponRecoilOffset > 0.001f || Mathf.Abs(weaponRecoilVelocity) > 0.001f)
			{
				float springK = 180.0f;
				float dampingC = 24.0f;
				float accel = (-springK * weaponRecoilOffset) - (dampingC * weaponRecoilVelocity);
				weaponRecoilVelocity += accel * delta;
				weaponRecoilOffset += weaponRecoilVelocity * delta;
				if (weaponRecoilOffset < 0f && Mathf.Abs(weaponRecoilVelocity) < 0.02f)
				{
					weaponRecoilOffset = 0f;
					weaponRecoilVelocity = 0f;
				}
				if (UnitKey != "artillery")
					weaponPitchNode.Position = weaponPitchRestPosition + new Vector3(0f, 0f, weaponRecoilOffset);
				else
					weaponPitchNode.Position = weaponPitchRestPosition;
			}
			else
			{
				weaponPitchNode.Position = weaponPitchRestPosition;
			}
		}

		// 载具脱战/非交火状态下炮塔平滑归中回正（对齐车身行进方向）
		if (AttackTarget is null && !AttackGrounding)
		{
			if (weaponYawNode is not null && IsNodeReadyForWorldAim(weaponYawNode) && weaponYawNode.Rotation.LengthSquared() > 0.0001f)
			{
				weaponYawNode.Rotation = SmoothEuler(weaponYawNode.Rotation, Vector3.Zero, delta, 3.5f);
			}
			if (weaponPitchNode is not null && IsNodeReadyForWorldAim(weaponPitchNode))
			{
				float restPitch = (UnitKey == "artillery" && DeployState == ArtilleryDeployState.Deployed)
					? Mathf.DegToRad(58f)
					: 0f;
				var targetRot = new Vector3(restPitch, 0f, 0f);
				if ((weaponPitchNode.Rotation - targetRot).LengthSquared() > 0.0001f)
				{
					weaponPitchNode.Rotation = SmoothEuler(weaponPitchNode.Rotation, targetRot, delta, 3.5f);
				}
			}
		}

		// 受击顿挫微冲量与物理恢复 (Hit Impulse & Shudder)
		if (hitFlashTimer > 0f)
		{
			hitFlashTimer = Mathf.Max(0f, hitFlashTimer - delta);
		}
		if (hitImpulseOffset.LengthSquared() > 0.0001f || hitImpulseVelocity.LengthSquared() > 0.0001f)
		{
			float returnFactor = 1.0f - Mathf.Exp(-22.0f * delta);
			hitImpulseVelocity = hitImpulseVelocity.Lerp(Vector3.Zero, returnFactor);
			hitImpulseOffset += hitImpulseVelocity * delta;
			hitImpulseOffset = hitImpulseOffset.Lerp(Vector3.Zero, returnFactor);

			hitTiltVelocity = Mathf.Lerp(hitTiltVelocity, 0f, returnFactor);
			hitTiltAngle += hitTiltVelocity * delta;
			hitTiltAngle = Mathf.Lerp(hitTiltAngle, 0f, returnFactor);

			if (visualRoot is not null)
			{
				visualRoot.Position = visualRootRestPosition + hitImpulseOffset;
			}
		}

		// 步兵写实视觉特效：行进微步扬尘、射击后坐力微动、迫击炮蹲姿联动
		if (BattleUnitCatalog.IsInfantryLike(UnitKey))
		{
			UpdateInfantryVisualEffects(actuallyMoving, delta);
		}

		// 如果是舰船或潜艇单位，在真实行驶/漂浮状态下更新水面浪花、波纹粒子及螺旋桨推进旋转
		if (BattleUnitCatalog.IsNavalUnit(UnitKey) || UnitKey == "submarine" || visualKey == "submarine")
		{
			UpdateNavalWaves(actuallyMoving, delta);
		}

		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			if (UnitKey == "scout_helicopter" || visualKey == "scout_helicopter")
			{
				// 直升机真实物理飞行姿态：前飞时机头前俯 8.5° 产生水平推进分力，悬停时平滑复位水平
				if (visualRoot is not null)
				{
					float targetPitch = actuallyMoving ? Mathf.DegToRad(-8.5f) : 0f;
					currentBankRoll = Mathf.LerpAngle(currentBankRoll, targetPitch, delta * 5.0f);
					var pitchBasis = new Basis(Vector3.Right, currentBankRoll);
					visualRoot.Basis = visualBaseBasis * pitchBasis;
				}

				// 直升机主旋翼与尾翼转速联动：悬停时 3.8x，前飞巡航时 5.6x，偏航转向时尾翼瞬时爆发抗扭力 (+1.5x)
				float yawDiff = Mathf.Abs(Mathf.AngleDifference(Rotation.Y, lastRecordedYaw));
				lastRecordedYaw = Rotation.Y;
				float yawBoost = Mathf.Clamp(yawDiff / Mathf.Max(0.001f, delta) * 0.14f, 0f, 1.6f);
				float targetHeliScale = parkedAtAirfield ? 1.0f : (actuallyMoving ? 5.6f : 4.0f) + yawBoost;
				foreach (var player in visualAnimationPlayers)
				{
					player.SpeedScale = Mathf.Lerp((float)player.SpeedScale, targetHeliScale, delta * 8.0f);
				}

				// 独立驱动尾旋翼沿其横向自转轴高速旋转，偏航转向时抗扭爆发
				if (helicopterTailRotor is not null && GodotObject.IsInstanceValid(helicopterTailRotor))
				{
					float tailSpinSpeed = (parkedAtAirfield ? 12.0f : (actuallyMoving ? 85.0f : 55.0f)) + yawBoost * 35.0f;
					helicopterTailRotor.RotateObjectLocal(Vector3.Back, tailSpinSpeed * delta);
				}
			}
			else
			{
				UpdateJetExhaust(actuallyMoving, delta);

				// 真实战机气动转弯倾斜（Bank Roll）与机头俯仰平滑 (无万向节锁纯净局部基底旋转)
				if (visualRoot is not null)
				{
					float yawDelta = Mathf.AngleDifference(Rotation.Y, lastRecordedYaw);
					lastRecordedYaw = Rotation.Y;
					float yawRate = yawDelta / Mathf.Max(0.001f, delta);

					// 重型战略轰炸机 (B-2) 依靠机翼阻力舵差动转向，转弯坡度极微 (<=4°)；高速战斗机 (F/A-18, F-22) 倾斜角温和真实 (<=14°)
					bool isHeavyBomber = UnitKey == "bomber" || visualKey == "bomber";
					float maxRollDeg = isHeavyBomber ? 4.0f : 14.0f;
					float targetRoll = (actuallyMoving && !parkedAtAirfield && Mathf.Abs(yawRate) > 0.02f)
						? Mathf.Clamp(yawRate * 0.14f, -Mathf.DegToRad(maxRollDeg), Mathf.DegToRad(maxRollDeg))
						: 0f;
					currentBankRoll = Mathf.LerpAngle(currentBankRoll, targetRoll, delta * 4.5f);

					// 在机身局部机头-机尾前向轴 (Vector3.Back 即局部 Z 轴) 施加无奇异平滑气动侧倾，彻底杜绝飞机侧立与侧滑
					var rollBasis = new Basis(Vector3.Back, currentBankRoll);
					visualRoot.Basis = visualBaseBasis * rollBasis;
				}
			}
		}

		// 战车悬挂后坐震荡物理模拟 (Damped Spring Oscillator)
		if (Mathf.Abs(chassisRecoilPitch) > 0.0005f || Mathf.Abs(chassisRecoilPitchVel) > 0.0005f)
		{
			float springK = 145.0f;
			float dampingC = 15.0f;
			float accel = (-springK * chassisRecoilPitch) - (dampingC * chassisRecoilPitchVel);
			chassisRecoilPitchVel += accel * delta;
			chassisRecoilPitch += chassisRecoilPitchVel * delta;
			if (Mathf.Abs(chassisRecoilPitch) < 0.0005f && Mathf.Abs(chassisRecoilPitchVel) < 0.005f)
			{
				chassisRecoilPitch = 0f;
				chassisRecoilPitchVel = 0f;
			}
		}

		// 地形坡度贴合与战车后坐俯仰联动 (Pitch & Roll Alignment + Chassis Recoil)
		if (visualRoot is not null && !BattleUnitCatalog.IsAirUnit(UnitKey) && !BattleUnitCatalog.IsNavalUnit(UnitKey))
		{
			Vector3 targetNormal = SampleGroundNormal();
			currentNormal = currentNormal.Lerp(targetNormal, 14f * delta).Normalized();

			var savedScale = visualRoot.Scale;
			Quaternion slopeQ = Quaternion.Identity;
			// 只有在地表坡度真正产生倾角（> 3.6°）时才生效，杜绝在平坦路面/演习靶场转弯时因浮点法线微差诱发的陀螺进动摆动
			if (currentNormal.Y < 0.998f)
			{
				Vector3 localNormal = (GlobalTransform.Basis.Inverse() * currentNormal).Normalized();
				float localPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localNormal.Z, -0.99f, 0.99f)), -Mathf.DegToRad(25f), Mathf.DegToRad(25f));
				float localRoll = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(localNormal.X, -0.99f, 0.99f)), -Mathf.DegToRad(20f), Mathf.DegToRad(20f));
				slopeQ = Quaternion.FromEuler(new Vector3(localPitch, 0f, localRoll));
			}

			var baseQ = visualBaseBasis.GetRotationQuaternion();
			if (Mathf.Abs(chassisRecoilPitch) > 0.0001f)
			{
				var recoilQ = new Quaternion(Vector3.Right, chassisRecoilPitch);
				visualRoot.Quaternion = slopeQ * recoilQ * baseQ;
			}
			else
			{
				visualRoot.Quaternion = slopeQ * baseQ;
			}
			visualRoot.Scale = savedScale;
		}
	}

	Vector3 SampleGroundNormal()
	{
		var world3D = GetWorld3D();
		if (world3D is null) return Vector3.Up;
		var spaceState = world3D.DirectSpaceState;
		if (spaceState is null) return Vector3.Up;

		var center = GlobalPosition;
		var query = PhysicsRayQueryParameters3D.Create(center + Vector3.Up * 10f, center + Vector3.Down * 25f);
		query.CollisionMask = 1;
		var hit = spaceState.IntersectRay(query);
		if (hit.Count > 0 && hit.ContainsKey("normal"))
		{
			var norm = hit["normal"].AsVector3();
			return norm.Y > 0.1f ? norm : Vector3.Up;
		}

		return IsOnFloor() ? GetFloorNormal() : Vector3.Up;
	}

	void UpdateAnimationPlayback(bool moving, float delta)
	{
		if (visualAnimationPlayers.Count == 0)
			return;

		if (visualAnimationLockRemaining > 0f)
		{
			visualAnimationLockRemaining = Mathf.Max(0f, visualAnimationLockRemaining - delta);
			return;
		}

		float planarSpeed = new Vector2(Velocity.X, Velocity.Z).Length();

		// 自然步态分层判定与动作选择 (Idle 战术待机 -> Walk 低速行军 -> Run 冲刺战术疾跑)
		string next;
		float targetSpeed;
		if (!moving || planarSpeed < 0.18f)
		{
			next = idleAnimationName;
			targetSpeed = 1.0f;
		}
		else if (planarSpeed < 2.85f && !string.IsNullOrEmpty(walkAnimationName))
		{
			// 慢速行走/微调行军：播放真实行走动画 (walking.fbx)，参考自然行进步速 1.35 m/s
			next = walkAnimationName;
			const float WalkRefSpeed = 1.35f;
			targetSpeed = Mathf.Clamp(planarSpeed / WalkRefSpeed, 0.65f, 1.45f);
		}
		else
		{
			// 高速机动/冲刺进攻：播放战术疾跑 (rifle run.fbx)，参考冲刺步速 4.20 m/s
			next = !string.IsNullOrEmpty(runAnimationName) ? runAnimationName : walkAnimationName;
			const float RunRefSpeed = 4.20f;
			targetSpeed = Mathf.Clamp(planarSpeed / RunRefSpeed, 0.65f, 1.40f);
		}

		if (visualKey != "scout_plane")
		{
			// 真实物理位移速度驱动步态播放速率：速度与动画参考步长严密对齐，根除滑步
			foreach (var player in visualAnimationPlayers)
			{
				player.SpeedScale = Mathf.Lerp((float)player.SpeedScale, targetSpeed, (float)delta * 14.0f);
			}
		}

		if (string.IsNullOrEmpty(next) || next == currentVisualAnimationName)
			return;

		float blendDuration = (next == walkAnimationName || next == runAnimationName) ? 0.15f : 0.20f;
		foreach (var player in visualAnimationPlayers)
		{
			if (player.HasAnimation(next))
				player.Play(next, customBlend: blendDuration);
		}
		currentVisualAnimationName = next;
	}

	/// <summary>
	/// 解析武器实际炮口/枪口的精准 3D 世界坐标与发射朝向向量，保证炮弹从炮管管口射出
	/// </summary>
	public (Vector3 Position, Vector3 Direction) GetMuzzleTransform(Vector3 targetPos)
	{
		Vector3 barrelDir;
		Vector3 muzzlePos;

		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			barrelDir = -GlobalTransform.Basis.Z;
			muzzlePos = GlobalPosition + barrelDir * 2.2f + Vector3.Down * 0.15f;
			return (muzzlePos, barrelDir);
		}

		if (BattleUnitCatalog.IsInfantryLike(UnitKey))
		{
			var toTarget = targetPos - GlobalPosition;
			toTarget.Y = 0f;
			barrelDir = toTarget.LengthSquared() > 0.001f ? toTarget.Normalized() : -GlobalTransform.Basis.Z;
			var right = barrelDir.Cross(Vector3.Up).Normalized();

			if (UnitKey == "infantry_artillery")
			{
				muzzlePos = GlobalPosition + Vector3.Up * 1.45f + barrelDir * 1.1f - right * 0.18f;
			}
			else if (UnitKey is "infantry_flamethrower" or "flamethrower")
			{
				muzzlePos = GlobalPosition + Vector3.Up * 0.75f + barrelDir * 0.95f - right * 0.15f;
			}
			else
			{
				muzzlePos = GlobalPosition + Vector3.Up * 0.72f + barrelDir * 0.75f - right * 0.15f;
			}
			return (muzzlePos, barrelDir);
		}

		// 载具、坦克、火炮与水面战舰炮管炮口计算
		if (UnitKey == "tank_destroyer")
		{
			// 坦克歼击车：主炮沿 -Z 延伸至 3.6m 炮口制退器
			if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				barrelDir = ResolveAimForward(weaponPitchNode, weaponPitchLookOffset);
				muzzlePos = weaponPitchNode.GlobalPosition + barrelDir * 3.6f;
				return (muzzlePos, barrelDir);
			}
		}
		else if (UnitKey == "heavy_tank")
		{
			// 重型坦克主炮 misc_b：沿 -Z 延伸 4.92 米
			if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				barrelDir = -weaponPitchNode.GlobalTransform.Basis.Z;
				muzzlePos = weaponPitchNode.ToGlobal(new Vector3(0f, 0f, -4.92f));
				return (muzzlePos, barrelDir);
			}
		}
		else if (UnitKey is "tank" or "medium_tank")
		{
			// 中型坦克 Panzer IV：GunPivot 内 barrel 沿 +Z 延伸至 1.09 米
			var barrelNode = weaponPitchNode?.FindChild("barrel", true, false) as Node3D;
			if (barrelNode is not null && GodotObject.IsInstanceValid(barrelNode))
			{
				barrelDir = ResolveAimForward(weaponPitchNode!, weaponPitchLookOffset);
				muzzlePos = barrelNode.ToGlobal(new Vector3(0f, 0f, 1.09f));
				return (muzzlePos, barrelDir);
			}
			else if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				barrelDir = ResolveAimForward(weaponPitchNode, weaponPitchLookOffset);
				muzzlePos = weaponPitchNode.GlobalPosition + barrelDir * 3.1f;
				return (muzzlePos, barrelDir);
			}
		}
		else if (UnitKey == "light_tank")
		{
			// 轻型坦克：炮管在模型空间沿 +Z 延伸
			if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				barrelDir = ResolveAimForward(weaponPitchNode, weaponPitchLookOffset);
				muzzlePos = weaponPitchNode.GlobalPosition + barrelDir * 2.2f;
				return (muzzlePos, barrelDir);
			}
		}
		else if (UnitKey == "artillery")
		{
			// 自行火炮：GunPivot 内 object_6 沿 -Z 延伸至炮口制退器 (约 4.82m)
			var barrelNode = weaponPitchNode?.FindChild("object_6", true, false) as Node3D;
			if (barrelNode is not null && GodotObject.IsInstanceValid(barrelNode))
			{
				var aabb = (barrelNode as MeshInstance3D)?.Mesh?.GetAabb() ?? new Aabb(Vector3.Zero, Vector3.One);
				muzzlePos = barrelNode.ToGlobal(new Vector3(0.36f, aabb.Position.Y + aabb.Size.Y * 0.5f, aabb.Position.Z));
				barrelDir = (muzzlePos - weaponPitchNode!.GlobalPosition).Normalized();
				return (muzzlePos, barrelDir);
			}
			else if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
			{
				barrelDir = ResolveAimForward(weaponPitchNode, weaponPitchPivotOffset);
				muzzlePos = weaponPitchNode.GlobalPosition + barrelDir * 3.8f;
				return (muzzlePos, barrelDir);
			}
		}
		else if (UnitKey == "battleship")
		{
			barrelDir = -GlobalTransform.Basis.Z;
			muzzlePos = GlobalPosition + barrelDir * 4.8f + Vector3.Up * 2.1f;
			return (muzzlePos, barrelDir);
		}
		else if (UnitKey == "destroyer_ship")
		{
			barrelDir = -GlobalTransform.Basis.Z;
			muzzlePos = GlobalPosition + barrelDir * 3.2f + Vector3.Up * 1.35f;
			return (muzzlePos, barrelDir);
		}

		// 通用装甲载具瞄准与仰角解析
		if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
		{
			barrelDir = ResolveAimForward(weaponPitchNode, weaponPitchLookOffset);
			muzzlePos = weaponPitchNode.GlobalPosition + barrelDir * 2.6f;
			return (muzzlePos, barrelDir);
		}
		if (weaponYawNode is not null && GodotObject.IsInstanceValid(weaponYawNode))
		{
			barrelDir = ResolveAimForward(weaponYawNode, weaponYawLookOffset);
			muzzlePos = weaponYawNode.GlobalPosition + barrelDir * 2.4f;
			return (muzzlePos, barrelDir);
		}

		var flatDir = targetPos - GlobalPosition;
		flatDir.Y = 0f;
		barrelDir = flatDir.LengthSquared() > 0.001f ? flatDir.Normalized() : -GlobalTransform.Basis.Z;
		muzzlePos = GlobalPosition + Vector3.Up * 0.95f + barrelDir * 2.0f;
		return (muzzlePos, barrelDir);
	}

	void PlayFireVisualAnimation(bool isGrenade = false)
	{
		TriggerInfantryFireVisuals(isGrenade);

		// 载具与火炮开火后坐力与炮口火光 (Muzzle Flash & Mechanical Recoil)
		if (!BattleUnitCatalog.IsInfantryLike(UnitKey))
		{
			if (UnitKey == "bomber")
			{
				// 轰炸机攻击属于机腹弹舱投掷重型航弹，由 SpawnAerialBomb 呈现专属投弹气浪特效，无需火炮炮口光与后坐
				return;
			}

			var targetPos = AttackTarget is not null && GodotObject.IsInstanceValid(AttackTarget)
				? AttackTarget.GlobalPosition
				: GlobalPosition - GlobalTransform.Basis.Z * 20f;
			var (muzzlePos, forward) = GetMuzzleTransform(targetPos);
			var color = PlayerOwned ? new Color(1.0f, 0.72f, 0.25f) : new Color(1.0f, 0.35f, 0.15f);
			var profile = UnitKey switch
			{
				"fighter" or "scout_plane" or "scout_helicopter" => "machinegun",
				"anti_air_gun" => "aa",
				"sam_missile" or "mlrs_rocket" or "artillery" => "artillery",
				"tank_destroyer" => "cannon",
				"battleship" or "destroyer_ship" or "submarine" or "patrol_boat" => "naval",
				_ => "cannon"
			};
			CombatProjectile.SpawnMuzzleFlash(this, muzzlePos, forward, color, false, profile);

			// 拟真重型战车开火底盘后坐俯仰 (Chassis Hull Recoil Pitch)
			if (UnitKey is "heavy_tank" or "battleship" or "tank_destroyer")
			{
				chassisRecoilPitch = -0.075f;
				chassisRecoilPitchVel = 1.6f;
			}
			else if (UnitKey is "artillery" or "mlrs_rocket")
			{
				chassisRecoilPitch = -0.095f;
				chassisRecoilPitchVel = 2.0f;
			}
			else if (UnitKey is "tank" or "medium_tank" or "destroyer_ship")
			{
				chassisRecoilPitch = -0.052f;
				chassisRecoilPitchVel = 1.25f;
			}
			else if (UnitKey is "light_tank" or "anti_air_gun" or "patrol_boat")
			{
				chassisRecoilPitch = -0.035f;
				chassisRecoilPitchVel = 0.95f;
			}
		}

		if (weaponPitchNode is not null && GodotObject.IsInstanceValid(weaponPitchNode))
		{
			if (!hasRecordedWeaponPitchRest)
			{
				weaponPitchRestPosition = weaponPitchNode.Position;
				hasRecordedWeaponPitchRest = true;
			}
			weaponRecoilOffset = BattleUnitCatalog.IsArtilleryLike(visualKey) ? 0.38f : 0.22f;
			weaponRecoilVelocity = -0.5f;
		}

		if (visualAnimationPlayers.Count == 0 || string.IsNullOrEmpty(fireAnimationName))
			return;

		foreach (var player in visualAnimationPlayers)
		{
			if (player.HasAnimation(fireAnimationName))
				player.Play(fireAnimationName);
		}
		currentVisualAnimationName = fireAnimationName;
		var animation = visualAnimationPlayers
			.Select(player => player.HasAnimation(fireAnimationName) ? player.GetAnimation(fireAnimationName) : null)
			.FirstOrDefault(candidate => candidate is not null);
		visualAnimationLockRemaining = animation is null
			? 0.18f
			: Mathf.Min(AttackCooldown * 0.85f, Mathf.Max(0.12f, (float)animation.Length * 0.92f));
	}

	void UpdateInfantryStride(bool moving)
	{
		if (infantryVisualParts.Count == 0)
			return;

		// 根据实际运动速度按比例调节步伐周期
		float speedRatio = Velocity.Length() / Mathf.Max(0.1f, MoveSpeed);
		var stride = moving ? visualMotionTime * (6.5f + 4.0f * speedRatio) : 0f;

		foreach (var part in infantryVisualParts)
		{
			var wave = Mathf.Sin(stride + part.Phase);
			var counterWave = Mathf.Sin(stride + part.Phase + Mathf.Pi);
			switch (part.Kind)
			{
				case InfantryVisualPartKind.LeftLeg:
					ApplyInfantryLimbStride(part, moving, wave, 0.42f);
					break;
				case InfantryVisualPartKind.RightLeg:
					ApplyInfantryLimbStride(part, moving, counterWave, 0.42f);
					break;
				case InfantryVisualPartKind.LeftArm:
					ApplyInfantryLimbStride(part, moving, counterWave, 0.30f);
					break;
				case InfantryVisualPartKind.RightArm:
					ApplyInfantryLimbStride(part, moving, wave, 0.30f);
					break;
				default:
					// 躯干上下起伏与偏航/侧倾 - 适当改小以防止士兵产生夸张的“弹簧震动”
					var bob = moving ? Mathf.Abs(wave) * 0.022f : 0f;
					var forwardStep = moving ? Mathf.Cos(stride + part.Phase) * 0.012f : 0f;
					var sideSway = moving ? Mathf.Sin(stride * 0.5f + part.Phase) * 0.006f : 0f;
					var lean = moving ? wave * 0.022f : 0f;
					part.Node.Position = part.RestPosition + new Vector3(sideSway, bob, forwardStep);
					part.Node.Rotation = part.RestRotation + new Vector3(lean * 0.45f, lean * 0.65f, lean);
					break;
			}
		}
	}

	static void ApplyInfantryLimbStride(AnimatedVisualPart part, bool moving, float wave, float swing)
	{
		if (!moving)
		{
			part.Node.Position = part.RestPosition;
			part.Node.Rotation = part.RestRotation;
			return;
		}

		// 仅在 Y 轴做极小的提足抬升（最大 0.015m），移除 Z 轴前后平移以防肢体关节脱臼分离
		float yOffset = (part.Kind == InfantryVisualPartKind.LeftLeg || part.Kind == InfantryVisualPartKind.RightLeg)
			? Mathf.Abs(wave) * 0.015f
			: 0f;

		part.Node.Position = part.RestPosition + new Vector3(0f, yOffset, 0f);
		// 主轴在 X 轴前后摆动，并提供微小的 Z 轴 Roll 偏角以保持身形协调而不再外八字外翻
		part.Node.Rotation = part.RestRotation + new Vector3(wave * swing, 0f, wave * swing * 0.04f);
	}

	void TriggerInfantryFireVisuals(bool isGrenade = false)
	{
		if (!BattleUnitCatalog.IsInfantryLike(UnitKey))
			return;

		infantryRecoil = 1.0f;

		if (isGrenade)
			return;

		var targetPos = AttackTarget is not null && GodotObject.IsInstanceValid(AttackTarget)
			? AttackTarget.GlobalPosition
			: (GlobalPosition - GlobalTransform.Basis.Z * 6f);
		var (muzzlePos, forward) = GetMuzzleTransform(targetPos);
		var right = forward.Cross(Vector3.Up).Normalized();

		var flashColor = PlayerOwned ? new Color(1.0f, 0.88f, 0.42f) : new Color(1.0f, 0.45f, 0.20f);
		if (UnitKey == "infantry_artillery")
		{
			CombatProjectile.SpawnBackblastEffect(this, GlobalPosition, forward);
			CombatProjectile.SpawnMuzzleFlash(this, muzzlePos, forward, flashColor, false, "mortar");
		}
		else if (UnitKey is "infantry_flamethrower" or "flamethrower")
		{
			CombatProjectile.SpawnMuzzleFlash(this, muzzlePos, forward, flashColor, true, "flame");
		}
		else
		{
			CombatProjectile.SpawnCartridgeEjection(this, muzzlePos, forward, right);
			CombatProjectile.SpawnMuzzleFlash(this, muzzlePos, forward, flashColor, false, "rifle");
		}
	}

	void UpdateInfantryVisualEffects(bool actuallyMoving, float delta)
	{
		// 1. 步兵行进踏步扬尘 (Footstep Dust)
		if (actuallyMoving && IsOnFloor() && !IsDead)
		{
			float speed = Velocity.Length();
			float stepRate = Mathf.Clamp(speed * 2.8f, 1.2f, 4.5f);
			infantryFootstepTimer += delta * stepRate;
			if (infantryFootstepTimer >= 1.0f)
			{
				infantryFootstepTimer = 0f;
				infantryFootstepSide = (infantryFootstepSide + 1) % 2;
				float sideSign = infantryFootstepSide == 0 ? -1f : 1f;
				Vector3 right = -GlobalTransform.Basis.X;
				Vector3 forward = -GlobalTransform.Basis.Z;

				if (visualRoot is not null && visualRoot.GetChildCount() > 0)
				{
					foreach (var child in visualRoot.GetChildren())
					{
						if (child is Node3D soldierNode)
						{
							Vector3 soldierPos = soldierNode.GlobalPosition;
							Vector3 footPos = soldierPos + right * (sideSign * 0.12f) - forward * 0.05f;
							footPos.Y = GlobalPosition.Y + 0.02f;
							CombatProjectile.SpawnFootstepDust(this, footPos);
						}
					}
				}
				else
				{
					Vector3 footPos = GlobalPosition + right * (sideSign * 0.14f) - forward * 0.05f;
					footPos.Y = GlobalPosition.Y + 0.02f;
					CombatProjectile.SpawnFootstepDust(this, footPos);
				}
			}
		}
		else
		{
			infantryFootstepTimer = 0.5f;
		}

		// 2. 迫击炮兵蹲姿与所有步兵开火后坐力微动混合
		bool isArtillery = visualKey == "infantry_artillery";
		if (isArtillery)
		{
			bool isFiring = visualAnimationLockRemaining > 0f && currentVisualAnimationName == fireAnimationName;
			float targetCrouch = isFiring ? 1f : 0f;
			crouchRatio = Mathf.Lerp(crouchRatio, targetCrouch, delta * 12f);
		}
		else
		{
			crouchRatio = 0f;
		}

		if (infantryRecoil > 0.001f)
		{
			infantryRecoil = Mathf.Lerp(infantryRecoil, 0f, delta * 16.0f);
		}

		// 3. 将蹲姿偏移与后坐力回弹融合应用到士兵模型节点
		if (visualRoot is not null && (crouchRatio > 0.001f || infantryRecoil > 0.001f))
		{
			foreach (var child in visualRoot.GetChildren())
			{
				if (child is Node3D soldierNode)
				{
					var modelNode = soldierNode.GetNodeOrNull<Node3D>("Model") ?? soldierNode.GetNodeOrNull<Node3D>("Body");
					if (modelNode is not null)
					{
						if (!modelNode.HasMeta("original_pos"))
						{
							modelNode.SetMeta("original_pos", modelNode.Position);
							modelNode.SetMeta("original_rot", modelNode.Rotation);
						}

						Vector3 origPos = modelNode.GetMeta("original_pos").AsVector3();
						Vector3 origRot = modelNode.GetMeta("original_rot").AsVector3();

						// 肩扛大炮射击姿态：沉稳屈膝抵肩微倾后坐（屈膝仅 0.05m，上身微倾抵肩 2.0°，彻底根除原先向前趴地22度的诡异折叠姿势）
						float crouchHeight = isArtillery ? (0.05f * crouchRatio) : (0.36f * crouchRatio);
						float crouchTilt = isArtillery ? (0.035f * crouchRatio) : (-0.39f * crouchRatio);
						float crouchForward = isArtillery ? (-0.02f * crouchRatio) : (-0.15f * crouchRatio);

						// 击发瞬间强烈后坐力回震 (后坐退 0.045m，身管与上半身自然微扬 4.3°)
						float kickback = 0.045f * infantryRecoil;
						float kickPitch = isArtillery ? (0.075f * infantryRecoil) : (-0.065f * infantryRecoil);

						modelNode.Position = origPos + new Vector3(0f, -crouchHeight, crouchForward + kickback);
						modelNode.Rotation = origRot + new Vector3(crouchTilt + kickPitch, 0f, 0f);
					}
				}
			}
		}
		else if (visualRoot is not null && crouchRatio <= 0.001f && infantryRecoil <= 0.001f)
		{
			foreach (var child in visualRoot.GetChildren())
			{
				if (child is Node3D soldierNode)
				{
					var modelNode = soldierNode.GetNodeOrNull<Node3D>("Model") ?? soldierNode.GetNodeOrNull<Node3D>("Body");
					if (modelNode is not null && modelNode.HasMeta("original_pos"))
					{
						Vector3 origPos = modelNode.GetMeta("original_pos").AsVector3();
						Vector3 origRot = modelNode.GetMeta("original_rot").AsVector3();
						if (modelNode.Position != origPos) modelNode.Position = origPos;
						if (modelNode.Rotation != origRot) modelNode.Rotation = origRot;
					}
				}
			}
		}
	}

	void UpdatePropellers(float delta, bool moving)
	{
		if (propellerNodes.Count == 0)
			return;

		var isSub = UnitKey == "submarine" || visualKey == "submarine";
		if (!moving && (isSub || BattleUnitCatalog.IsNavalUnit(UnitKey)))
			return;

		var speed = isSub ? 42f : (parkedAtAirfield ? 11f : moving ? 58f : 36f);

		foreach (var propeller in propellerNodes)
		{
			propeller.RotateObjectLocal(Vector3.Forward, speed * delta);
		}
	}

	void ClearQueuedMovement()
	{
		queuedMoveTargets.Clear();
		holdPosition = false;
	}

	void ClearSpecialOrders()
	{
		CancelBombingRun();
		parkingAtAirfield = false;
		parkedAtAirfield = false;
	}

	void CancelBombingRun()
	{
		bombingRunActive = false;
		bombingRunOnAttackLeg = false;
		bombingRunVolleyReleased = false;
		bombingRunReleaseDistance = 0f;
	}

	void ParkAtAirfield()
	{
		parkingAtAirfield = false;
		parkedAtAirfield = true;
		TargetPosition = parkedPosition;
		Velocity = Vector3.Zero;
		GlobalPosition = new Vector3(parkedPosition.X, AirfieldParkingHeight, parkedPosition.Z);
	}

	async void ReleaseBombingRunVolley()
	{
		bombingRunVolleyReleased = true;
		if (MaxAmmo > 0)
		{
			CurrentAmmo = Mathf.Max(0, CurrentAmmo - 1);
		}
		var bombDamage = Mathf.Max(1f, AttackDamage * BomberDamageMultiplier);

		int totalBombs = 5;
		for (int i = 0; i < totalBombs; i++)
		{
			if (!GodotObject.IsInstanceValid(this) || IsDead) break;

			var flatPlane = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
			float forwardInertiaLead = MoveSpeed * 1.15f;
			float sideJitter = (i % 2 == 0 ? -1f : 1f) * 1.25f;
			Vector3 impactPoint = flatPlane + bombingRunDirection * forwardInertiaLead + bombingRunPerpendicular * sideJitter;
			impactPoint.Y = 0f;

			CombatProjectile.SpawnAerialBomb(this, this, impactPoint, bombDamage, PlayerOwned);

			// 间隔 0.12 秒连续下挂投弹，在空中形成从机腹投掷弹药自由落体、地面地毯式爆炸波效果
			await ToSignal(GetTree().CreateTimer(0.12f), SceneTreeTimer.SignalName.Timeout);
		}
	}

	Vector3 ComputeBombingRunJitter(int bombIndex)
	{
		var seed = NetId != 0 ? NetId : (int)(GetInstanceId() & 0x7fffffff);
		var noiseA = Mathf.Sin((seed * 0.731f + bombIndex * 1.913f + 0.37f) * 12.9898f);
		var noiseB = Mathf.Sin((seed * 1.117f + bombIndex * 2.357f + 1.19f) * 78.233f);
		return bombingRunDirection * (noiseA * BomberImpactJitter)
			 + bombingRunPerpendicular * (noiseB * BomberImpactJitter);
	}

	void PreparePanzerTurretRig(Node3D root)
	{
		if (root.FindChild("TurretPivot", true, false) is not null)
			return;

		var turretAnchor = root.FindChild("turret_exterior", true, false) as Node3D
			?? root.FindChild("mantlet_inner", true, false) as Node3D;
		if (turretAnchor is null)
			return;

		var rigRoot = turretAnchor.GetParent() as Node3D;
		if (rigRoot is null)
			return;

		var gunPivotAnchor = root.FindChild("mantlet_inner", true, false) as Node3D
			?? root.FindChild("barrel", true, false) as Node3D;
		var gunAnchor = root.FindChild("barrel", true, false) as Node3D;
		if (gunPivotAnchor is null || gunAnchor is null)
			return;

		var turretAnchorPos = turretAnchor.Position;

		var turretPivot = new Node3D
		{
			Name = "TurretPivot",
			Position = turretAnchorPos
		};
		rigRoot.AddChild(turretPivot);

		var gunPivot = new Node3D
		{
			Name = "GunPivot",
			Position = gunPivotAnchor.Position - turretAnchorPos
		};
		turretPivot.AddChild(gunPivot);

		var candidates = rigRoot.GetChildren().OfType<Node3D>().ToArray();
		foreach (var child in candidates)
		{
			if (child == turretPivot)
				continue;
			if (!BelongsToPanzerTurret(child.Name))
				continue;

			var originalPosition = child.Position;
			rigRoot.RemoveChild(child);
			if (BelongsToPanzerGun(child.Name))
			{
				gunPivot.AddChild(child);
				child.Position = originalPosition - turretAnchorPos - gunPivot.Position;
			}
			else
			{
				turretPivot.AddChild(child);
				child.Position = originalPosition - turretAnchorPos;
			}
		}
	}

	static void HidePanzerInteriorVisuals(Node3D root)
	{
		foreach (var mesh in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			if (IsPanzerInteriorVisual(mesh.Name))
				mesh.Visible = false;
		}
	}

	static bool IsPanzerExteriorTurretDetail(string lower)
	{
		return lower is "turret_exterior"
			|| lower.Contains("turret_door")
			|| lower.Contains("turret_sideslit")
			|| (lower.Contains("hatch") && !lower.Contains("driver") && !lower.Contains("radioman"))
			|| lower.Contains("cmdr_hatch")
			|| lower.Contains("cupola")
			|| lower.Contains("lever_gunner_door")
			|| lower.Contains("lever_loader_door")
			|| lower.StartsWith("mantlet")
			|| lower.StartsWith("barrel")
			|| lower is "gun";
	}

	static bool IsPanzerInteriorVisual(string name)
	{
		var lower = name.ToLowerInvariant();
		if (IsPanzerExteriorTurretDetail(lower))
			return false;

		return lower is "floor"
			or "floor_circle"
			or "recoil_guard"
			or "electricals"
			or "gyro"
			or "trigger"
			or "wires"
			or "driver_panel"
			or "cupola_details"
			or "gunner_sight"
			or "driver_block"
			or "driver_slit"
			or "radioman_hatch"
			or "driver_hatch"
			or "hull_sideport_driver"
			or "cupola_exterior"
			or "cupola_rim"
			or "gunner_front_port"
			or "loader_front_port"
			|| lower.StartsWith("turret_interior")
			|| lower.StartsWith("turret_details")
			|| lower.StartsWith("cmdr_hatch_")
			|| lower.StartsWith("turret_door_")
			|| lower.StartsWith("turret_sideslit_")
			|| lower.StartsWith("breech")
			|| lower.StartsWith("ammo_bin_")
			|| lower.StartsWith("shell.")
			|| lower.StartsWith("mg_ammo_")
			|| lower.StartsWith("lever_")
			|| lower.StartsWith("switch_")
			|| lower.StartsWith("gunner_")
			|| lower.StartsWith("loader_");
	}

	void PrepareArtilleryGunRig(Node3D root)
	{
		if (root is null || root is RtsUnit)
			return;

		if (root.GetNodeOrNull<Node3D>("GunPivot") is not null)
			return;

		MeshInstance3D? barrelCandidate = root.GetNodeOrNull<MeshInstance3D>("object_6")
			?? root.FindChild("object_6", true, false) as MeshInstance3D;

		if (barrelCandidate is null)
			return;

		// 真实榴弹炮耳轴铰链 (Trunnion Pivot Hinge):
		// object_6 局部耳轴几何中心：X=0.0018m, Y=-0.0185m, Z=0.0225m (在 GLB 局部节点尺度下)
		// 炮管以此为轴心仰起时，炮闩后部与炮车底盘支架完美咬合，炮管可仰起至 65° 高曲射仰角状态
		var trunnionLocal = new Vector3(0.0018f, -0.0185f, 0.0225f);
		var pivot = new Node3D
		{
			Name = "GunPivot",
			Position = trunnionLocal
		};
		root.AddChild(pivot);
		root.RemoveChild(barrelCandidate);
		pivot.AddChild(barrelCandidate);
		barrelCandidate.Position = -trunnionLocal;
	}

	static bool BelongsToPanzerTurret(string name)
	{
		var lower = name.ToLowerInvariant();
		return !IsPanzerInteriorVisual(lower) && IsPanzerExteriorTurretDetail(lower);
	}

	static bool BelongsToPanzerGun(string name)
	{
		var lower = name.ToLowerInvariant();
		return lower.StartsWith("barrel")
			|| lower.StartsWith("mantlet")
			|| lower is "gun";
	}

	public static void FixVehicleMeshMaterials(Node3D root, Color defaultTint)
	{
		if (root is null) return;

		foreach (var mesh in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
		{
			var meshName = mesh.Name.ToString();
			if (meshName.Contains("Blur", StringComparison.OrdinalIgnoreCase) || meshName.Contains("Disc", StringComparison.OrdinalIgnoreCase))
				continue;

			var meshObj = mesh.Mesh;
			if (meshObj is null) continue;

			var lowerMeshName = meshName.ToLowerInvariant();
			int surfCount = meshObj.GetSurfaceCount();
			for (int s = 0; s < surfCount; s++)
			{
				var existingMat = (mesh.GetSurfaceOverrideMaterial(s) ?? mesh.GetActiveMaterial(s) ?? meshObj.SurfaceGetMaterial(s)) as BaseMaterial3D;
				
				// 判定该表面是否为 GLTF 缺失材质的空占位表面（导入器自动生成 resource_name 为空、无贴图且为全白 1,1,1）
				bool isMissingOrPlaceholder = existingMat is null 
					|| (string.IsNullOrEmpty(existingMat.ResourceName) 
						&& existingMat.AlbedoTexture is null 
						&& existingMat.AlbedoColor.R >= 0.95f 
						&& existingMat.AlbedoColor.G >= 0.95f 
						&& existingMat.AlbedoColor.B >= 0.95f);

				if (isMissingOrPlaceholder)
				{
					// 核心修复：针对 GLTF 缺失材质的表面，根据部件语义赋予真实实体军工材质，杜绝空白或透明
					var solidMat = new StandardMaterial3D
					{
						Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
						CullMode = BaseMaterial3D.CullModeEnum.Disabled,
						DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always
					};

					if (lowerMeshName.Contains("barrel") || lowerMeshName.Contains("gun") || lowerMeshName.Contains("cannon") || lowerMeshName.Contains("weapon"))
					{
						// 坦克/火炮主炮管：冷轧合金重钢 (Gunmetal Steel)，深灰泛金属光泽，100% 实体无穿透
						solidMat.AlbedoColor = new Color(0.20f, 0.21f, 0.20f, 1f);
						solidMat.Metallic = 0.65f;
						solidMat.Roughness = 0.38f;
					}
					else if (lowerMeshName.Contains("cable") || lowerMeshName.Contains("wire") || lowerMeshName.Contains("hull_details") || lowerMeshName.Contains("detail") || lowerMeshName.Contains("tool"))
					{
						// 车体随车工具、牵引钢缆与五金构件：中灰工具钢
						solidMat.AlbedoColor = new Color(0.26f, 0.27f, 0.26f, 1f);
						solidMat.Metallic = 0.50f;
						solidMat.Roughness = 0.50f;
					}
					else
					{
						// 炮塔外装甲 (turret_exterior)、炮盾内衬 (mantlet_inner)、车长指挥塔 (cupola)、观察窗块与储物箱：统一采用军用防空涂装装甲色 (Dunkelgelb/军工绿)
						// 严密契合四号坦克原始 DarkYellow 基础色 (0.406, 0.378, 0.246)，杜绝纯白反光与透明穿透
						solidMat.AlbedoColor = new Color(0.406f, 0.378f, 0.246f, 1f);
						solidMat.Metallic = 0.15f;
						solidMat.Roughness = 0.65f;
					}

					mesh.SetSurfaceOverrideMaterial(s, solidMat);
				}
				else
				{
					// 核心修复：战车所有已有材质的实体部件（包括重型坦克、轻型悬浮坦克、四号坦克其余表面），强制禁用 Alpha 混合，
					// 彻底解除 GLTF alphaMode: BLEND 导致的深度写入禁用 (DEPTH_DRAW_OPAQUE_ONLY) 引起的背向穿透与透明幽灵感
					var dupMat = existingMat?.Duplicate() as BaseMaterial3D;
					if (dupMat is not null)
					{
						dupMat.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
						dupMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
						dupMat.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always;
						var c = dupMat.AlbedoColor;
						dupMat.AlbedoColor = new Color(c.R, c.G, c.B, 1f);
						mesh.SetSurfaceOverrideMaterial(s, dupMat);
					}
				}
			}
		}
	}

	public void DebugDumpPanzerVisibleNodes()
	{
		if (visualRoot is not Node3D root)
			return;

		if (visualKey is not "tank" and not "medium_tank")
			return;

		foreach (var node in EnumerateDebugNodes(root))
		{
			if (node is not VisualInstance3D visual || !visual.Visible)
				continue;

			var lower = node.Name.ToString().ToLowerInvariant();
			if (!lower.Contains("turret")
				&& !lower.Contains("cupola")
				&& !lower.Contains("hatch")
				&& !lower.Contains("door")
				&& !lower.Contains("barrel")
				&& !lower.Contains("mantlet")
				&& !lower.Contains("floor")
				&& !lower.Contains("breech")
				&& !lower.Contains("gunner")
				&& !lower.Contains("loader")
				&& !lower.Contains("driver"))
				continue;

			GD.Print($"PANZER_VISIBLE {Name} :: {node.Name} class={node.GetClass()} parent={node.GetParent()?.Name} pos={node.Position} gpos={node.GlobalPosition}");
		}

		static IEnumerable<Node3D> EnumerateDebugNodes(Node3D parent)
		{
			foreach (var child in parent.GetChildren().OfType<Node3D>())
			{
				yield return child;
				foreach (var descendant in EnumerateDebugNodes(child))
					yield return descendant;
			}
		}
	}

	public bool CanAttackTarget(Node3D? target)
	{
		if (target is null || !GodotObject.IsInstanceValid(target))
			return false;

		if (target is RtsUnit enemyUnit)
		{
			if (enemyUnit.PlayerOwned == PlayerOwned || enemyUnit.IsDead)
				return false;
			return BattleUnitCatalog.CanAttackTargetType(UnitKey, enemyUnit.UnitKey);
		}

		if (target is RtsBuilding enemyBuilding)
		{
			if (enemyBuilding.PlayerOwned == PlayerOwned || enemyBuilding.Health <= 0f)
				return false;
			return BattleUnitCatalog.GetDamageMultiplierToBuilding(UnitKey) > 0f;
		}

		return false;
	}

	bool IsLiveEnemyTarget(Node3D target)
		=> target switch
		{
			RtsUnit unit => unit.PlayerOwned != PlayerOwned && !unit.IsDead && unit.FogRevealed,
			RtsBuilding building => building.PlayerOwned != PlayerOwned && building.Health > 0f && building.FogRevealed,
			_ => GodotObject.IsInstanceValid(target)
		};

	float GetUnitHealthBarHeight()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
			return 2.95f;
		if (UnitKey.StartsWith("infantry"))
			return 2.05f; // 步兵血条位于 2.05m 高度，精准悬浮于步兵头顶上方
		if (UnitKey.Contains("light_tank"))
			return 1.65f;
		return 2.05f; // 战车血条 2.05m 高度
	}

	float GetUnitHealthBarWidth()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
			return 2.8f;
		if (UnitKey.StartsWith("infantry"))
			return 0.9f; // 步兵血条缩窄为0.9m宽，匹配身形
		return 2.2f;
	}

	void EnsureCombatOverlays()
	{
		var healthBar = GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar");
		if (healthBar is null)
		{
			var isAir = BattleUnitCatalog.IsAirUnit(UnitKey);
			var bar = new WorldHealthBar3D
			{
				Name = "WorldHealthBar",
				Width = GetUnitHealthBarWidth(),
				HeightOffset = GetUnitHealthBarHeight(),
				Depth = isAir ? 0.20f : 0.18f,
				Position = SelectionRingPosition() + new Vector3(0f, GetUnitHealthBarHeight(), 0f)
			};
			AddChild(bar);
		}
		else
		{
			healthBar.Position = SelectionRingPosition() + new Vector3(0f, GetUnitHealthBarHeight(), 0f);
		}

		selectionRing = GetNodeOrNull<MeshInstance3D>("SelectionRing");
		if (selectionRing is null)
		{
			selectionRing = new MeshInstance3D
			{
				Name = "SelectionRing",
				Position = SelectionRingPosition(),
				Mesh = new TorusMesh
				{
					InnerRadius = SelectionRingRadius() * 0.90f,
					OuterRadius = SelectionRingRadius(),
					RingSegments = 48
				},
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = PlayerOwned
						? new Color(0.18f, 0.95f, 0.85f, 0.92f)
						: new Color(1f, 0.26f, 0.22f, 0.92f),
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled,
					DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
					RenderPriority = 1
				},
				Scale = SelectionRingScale(),
				Visible = false
			};
			AddChild(selectionRing);

			// 内部同心微光刻度环
			var innerRing = new MeshInstance3D
			{
				Name = "InnerRing",
				Mesh = new TorusMesh
				{
					InnerRadius = SelectionRingRadius() * 0.52f,
					OuterRadius = SelectionRingRadius() * 0.58f,
					RingSegments = 36
				},
				Position = Vector3.Zero,
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = PlayerOwned
						? new Color(0.18f, 0.95f, 0.85f, 0.45f)
						: new Color(1f, 0.26f, 0.22f, 0.45f),
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled,
					DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
					RenderPriority = 1
				}
			};
			selectionRing.AddChild(innerRing);

			// 战术朝向刻度箭头
			headingNotch = new MeshInstance3D
			{
				Name = "HeadingNotch",
				Position = new Vector3(0f, 0.01f, -SelectionRingRadius() * 0.96f),
				Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.02f, 0.28f) },
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = PlayerOwned
						? new Color(0.40f, 1.0f, 0.90f, 0.95f)
						: new Color(1f, 0.35f, 0.30f, 0.95f),
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled,
					DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
					RenderPriority = 2
				}
			};
			selectionRing.AddChild(headingNotch);
		}
		else
		{
			selectionRing.Scale = SelectionRingScale();
		}

		if (BattleUnitCatalog.IsAirUnit(UnitKey))
		{
			groundShadowRing = GetNodeOrNull<MeshInstance3D>("GroundShadowRing");
			if (groundShadowRing is null)
			{
				groundShadowRing = new MeshInstance3D
				{
					Name = "GroundShadowRing",
					TopLevel = true,
					Mesh = new TorusMesh
					{
						InnerRadius = SelectionRingRadius() * 0.82f,
						OuterRadius = SelectionRingRadius(),
						RingSegments = 36
					},
					MaterialOverride = new StandardMaterial3D
					{
						AlbedoColor = PlayerOwned
							? new Color(0.18f, 0.95f, 0.85f, 0.55f)
							: new Color(1f, 0.26f, 0.22f, 0.55f),
						Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
						ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
						CullMode = BaseMaterial3D.CullModeEnum.Disabled,
						DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Always,
						RenderPriority = 1
					},
					Visible = false
				};
				AddChild(groundShadowRing);
			}
		}

		techRing = GetNodeOrNull<MeshInstance3D>("TechRing");
		if (techRing is null)
		{
			techRing = new MeshInstance3D
			{
				Name = "TechRing",
				Position = TechRingPosition(),
				Mesh = new TorusMesh
				{
					InnerRadius = SelectionRingRadius() + 0.18f,
					OuterRadius = SelectionRingRadius() + 0.28f,
					RingSegments = 48
				},
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = new Color(0.38f, 1f, 0.50f, 0.54f),
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
				},
				Scale = SelectionRingScale(),
				Visible = false
			};
			AddChild(techRing);
		}
		else
		{
			techRing.Scale = SelectionRingScale();
		}
	}

	void RefreshCombatOverlays()
	{
		if (GetNodeOrNull<WorldHealthBar3D>("WorldHealthBar") is { } worldHealthBar)
			worldHealthBar.Position = SelectionRingPosition() + new Vector3(0f, GetUnitHealthBarHeight(), 0f);

		selectionRing ??= GetNodeOrNull<MeshInstance3D>("SelectionRing");
		if (selectionRing?.Mesh is TorusMesh selectionMesh)
		{
			selectionRing.Position = SelectionRingPosition();
			selectionRing.Scale = SelectionRingScale();
			selectionMesh.InnerRadius = SelectionRingRadius();
			selectionMesh.OuterRadius = SelectionRingRadius() + 0.10f;
		}

		techRing ??= GetNodeOrNull<MeshInstance3D>("TechRing");
		if (techRing?.Mesh is TorusMesh techMesh)
		{
			techRing.Position = TechRingPosition();
			techRing.Scale = SelectionRingScale();
			techMesh.InnerRadius = SelectionRingRadius() + 0.18f;
			techMesh.OuterRadius = SelectionRingRadius() + 0.28f;
		}
	}

	void ResetCapturedBaseStats()
	{
		baseMaxHealth = 0f;
		baseMoveSpeed = 0f;
		baseAttackDamage = 0f;
		baseAttackRange = 0f;
		baseAttackCooldown = 0f;
		baseSplashRadius = 0f;
		baseSplashFalloff = 0f;
	}

	void CaptureBaseStats()
	{
		if (baseMaxHealth > 0f)
			return;

		baseMaxHealth = MaxHealth;
		baseMoveSpeed = MoveSpeed;
		baseAttackDamage = AttackDamage;
		baseAttackRange = AttackRange;
		baseAttackCooldown = AttackCooldown;
		if (SplashRadius <= 0.05f)
			SplashRadius = BattleUnitCatalog.SplashRadius(UnitKey);
		if (SplashFalloff <= 0f || SplashFalloff > 1f)
			SplashFalloff = BattleUnitCatalog.SplashFalloff(UnitKey);
		baseSplashRadius = SplashRadius;
		baseSplashFalloff = SplashFalloff;
	}

	void UpdateTechBuffs(float delta)
	{
		if (techBuffs.Count == 0)
			return;

		var changed = false;
		for (var i = techBuffs.Count - 1; i >= 0; i--)
		{
			techBuffs[i].Remaining -= delta;
			if (techBuffs[i].Remaining > 0f)
				continue;
			techBuffs.RemoveAt(i);
			changed = true;
		}

		if (techRegenPerSecond > 0f && Health < MaxHealth)
		{
			techRegenCarry += techRegenPerSecond * delta;
			if (techRegenCarry >= 1f)
			{
				var repair = Mathf.Floor(techRegenCarry);
				techRegenCarry -= repair;
				Repair(repair, false);
			}
		}
		else
		{
			techRegenCarry = 0f;
		}

		if (changed)
			RecalculateTechBuffStats();
	}

	void RecalculateTechBuffStats()
	{
		CaptureBaseStats();
		var healthRatio = MaxHealth > 0f ? Mathf.Clamp(Health / MaxHealth, 0f, 1f) : 1f;
		var moveMultiplier = 1f;
		var damageMultiplier = 1f;
		var attackRangeBonus = 0f;
		var attackCooldownMultiplier = 1f;
		var defenseReduction = 0f;
		var visionBonus = 0f;
		var regenPerSecond = 0f;

		for (var i = 0; i < techBuffs.Count; i++)
		{
			var buff = techBuffs[i];
			moveMultiplier *= buff.MoveMultiplier;
			damageMultiplier *= buff.DamageMultiplier;
			attackRangeBonus += buff.AttackRangeBonus;
			attackCooldownMultiplier *= buff.AttackCooldownMultiplier;
			defenseReduction += buff.DefenseReduction;
			visionBonus += buff.VisionBonus;
			regenPerSecond += buff.RegenPerSecond;
		}

		MaxHealth = baseMaxHealth;
		MoveSpeed = baseMoveSpeed * moveMultiplier;
		AttackDamage = Mathf.Round(baseAttackDamage * damageMultiplier);
		AttackRange = baseAttackRange + attackRangeBonus;
		AttackCooldown = Mathf.Max(0.12f, baseAttackCooldown * attackCooldownMultiplier);
		SplashRadius = baseSplashRadius;
		SplashFalloff = baseSplashFalloff;
		VisionBonus = visionBonus;
		techDamageReduction = Mathf.Clamp(defenseReduction, 0f, 0.75f);
		techRegenPerSecond = regenPerSecond;
		Health = Mathf.Clamp(MaxHealth * healthRatio, 1f, MaxHealth);

		if (techBuffs.Count > 0 && techRing?.MaterialOverride is StandardMaterial3D ringMaterial)
			ringMaterial.AlbedoColor = techBuffs[^1].Tint;
		SetTechRingVisible(techBuffs.Count > 0);
	}

	void SetTechRingVisible(bool visible)
	{
		if (techRing is not null)
			techRing.Visible = visible;
	}

	Vector3 SelectionRingScale()
	{
		if (UnitKey == "aircraft_carrier" || UnitKey.Contains("carrier"))
			return new Vector3(1.15f, 1.0f, 2.45f);
		if (UnitKey == "submarine" || visualKey == "submarine")
			return new Vector3(1.10f, 1.0f, 2.10f);
		if (BattleUnitCatalog.IsNavalUnit(UnitKey))
			return new Vector3(1.10f, 1.0f, 1.85f);
		return Vector3.One;
	}

	Vector3 TechRingPosition()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
			return new Vector3(0f, -0.28f, 0f);
		return new Vector3(0f, 0.09f, 0f);
	}

	Vector3 SelectionRingPosition()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
			return new Vector3(0f, -0.30f, 0f);
		return new Vector3(0f, 0.08f, 0f);
	}

	float SelectionRingRadius()
	{
		if (BattleUnitCatalog.IsAirUnit(UnitKey))
			return 2.2f;
		if (UnitKey.StartsWith("infantry"))
			return 0.68f;
		if (UnitKey.Contains("light_tank") || UnitKey.Contains("jeep") || UnitKey.Contains("apc"))
			return 1.35f;
		if (UnitKey.Contains("heavy_tank") || UnitKey.Contains("artillery") || UnitKey.Contains("mlrs"))
			return 1.95f;
		if (UnitKey == "aircraft_carrier" || UnitKey.Contains("carrier"))
			return 4.6f;
		if (BattleUnitCatalog.IsNavalUnit(UnitKey))
			return 2.6f;
		return 1.55f;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (selectionRing is not null)
		{
			selectionRing.Visible = Selected && (PlayerOwned || FogRevealed);
			if (Selected)
				selectionRing.RotateY(0.35f * dt);
		}

		if (groundShadowRing is not null)
		{
			groundShadowRing.Visible = Selected && BattleUnitCatalog.IsAirUnit(UnitKey) && (PlayerOwned || FogRevealed);
			if (groundShadowRing.Visible)
			{
				groundShadowRing.GlobalPosition = new Vector3(GlobalPosition.X, 0.08f, GlobalPosition.Z);
				groundShadowRing.RotateY(-0.25f * dt);
			}
		}

		if (MaxShield > 0f && Shield < MaxShield && !IsDead)
		{
			secondsSinceLastDamage += dt;
			if (secondsSinceLastDamage >= 5.0f)
			{
				Shield = Mathf.Min(MaxShield, Shield + MaxShield * 0.1f * dt);
			}
		}
	}

	/// <summary>
	/// 通过对 visualRoot 下的所有 GeometryInstance3D 子节点临时叠加材质，
	/// 模拟战争迷雾的颜色调制效果（3D 节点不支持 Modulate）。
	/// </summary>
	void SetVisualModulate(Color color)
	{
		var root = visualRoot ?? this as Node3D;
		if (root is null)
			return;
		SetNodeModulateRecursive(root, color);
	}

	static void SetNodeModulateRecursive(Node3D node, Color color)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is GeometryInstance3D geom)
			{
				if (color == Colors.White)
				{
					// 恢复正常：移除覆盖材质
					geom.MaterialOverlay = null;
				}
				else
				{
					// 叠加半透明幽灵材质
					if (geom.MaterialOverlay is not StandardMaterial3D overlay
						|| overlay.ResourceName != "_fog_ghost_overlay")
					{
						overlay = new StandardMaterial3D
						{
							ResourceName = "_fog_ghost_overlay",
							Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
							ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
							BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
							NoDepthTest = false
						};
						geom.MaterialOverlay = overlay;
					}
					overlay.AlbedoColor = color;
				}
			}
			if (child is Node3D child3d)
				SetNodeModulateRecursive(child3d, color);
		}
	}

	/// <summary>
	/// 确保舰船行驶的两种水面动态特效（船头浪花和船尾水平扩散波纹）已被初始化并添加为子节点
	/// </summary>
	void EnsureNavalWaveParticles()
	{
		if (bowWaveParticles is null)
		{
			var splashMesh = new SphereMesh
			{
				Radius = 0.22f,
				Height = 0.44f,
				RadialSegments = 6,
				Rings = 3
			};

			var splashMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.9f, 0.95f, 1f, 0.72f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				RenderPriority = -15,
				DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
			};

			var splashScale = new Curve();
			splashScale.AddPoint(new Vector2(0f, 0.5f));
			splashScale.AddPoint(new Vector2(1f, 1.5f));

			var splashColor = CombatProjectile.CreateRamp(
				new Color(0.9f, 0.95f, 1f, 0.78f), 0.7f,
				new Color(0.85f, 0.92f, 0.96f, 0.35f),
				new Color(0.8f, 0.9f, 0.95f, 0f));

			var halfLength = 1.1f;
			if (UnitKey == "aircraft_carrier")
				halfLength = 4.5f;
			else if (UnitKey == "destroyer_ship")
				halfLength = 1.6f;
			else if (UnitKey == "transport_ship")
				halfLength = 1.9f;

			bowWaveParticles = new CpuParticles3D
			{
				Name = "BowWaveParticles",
				Amount = 14,
				Lifetime = 0.55f,
				OneShot = false,
				Direction = new Vector3(0f, 0.2f, 0.8f),
				Spread = 35f,
				Gravity = new Vector3(0f, -2.5f, 0f),
				InitialVelocityMin = 1.8f,
				InitialVelocityMax = 3.2f,
				ScaleAmountMin = 0.4f,
				ScaleAmountMax = 1.0f,
				Mesh = splashMesh,
				MaterialOverride = splashMat,
				ScaleAmountCurve = splashScale,
				ColorRamp = splashColor,
				Position = new Vector3(0f, 0.05f, -halfLength),
				Emitting = false
			};
			AddChild(bowWaveParticles);
		}

		if (sternWakeParticles is null)
		{
			var rippleMesh = new QuadMesh
			{
				Size = new Vector2(1f, 1f),
				Orientation = PlaneMesh.OrientationEnum.Y
			};

			var rippleMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.8f, 0.92f, 0.98f, 0.42f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				RenderPriority = -15,
				DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled
			};

			var rippleScale = new Curve();
			rippleScale.AddPoint(new Vector2(0f, 0.8f));
			rippleScale.AddPoint(new Vector2(1f, 3.4f));

			var rippleColor = CombatProjectile.CreateRamp(
				new Color(0.85f, 0.95f, 1f, 0.52f), 0.5f,
				new Color(0.8f, 0.9f, 0.96f, 0.22f),
				new Color(0.75f, 0.85f, 0.9f, 0f));

			var halfLength = 1.1f;
			if (UnitKey == "aircraft_carrier")
				halfLength = 4.5f;
			else if (UnitKey == "destroyer_ship")
				halfLength = 1.6f;
			else if (UnitKey == "transport_ship")
				halfLength = 1.9f;

			sternWakeParticles = new CpuParticles3D
			{
				Name = "SternWakeParticles",
				Amount = 10,
				Lifetime = 0.92f,
				OneShot = false,
				Direction = new Vector3(0f, 0f, 1f),
				Spread = 12f,
				Gravity = Vector3.Zero,
				InitialVelocityMin = 1.0f,
				InitialVelocityMax = 2.0f,
				ScaleAmountMin = 0.8f,
				ScaleAmountMax = 1.4f,
				Mesh = rippleMesh,
				MaterialOverride = rippleMat,
				ScaleAmountCurve = rippleScale,
				ColorRamp = rippleColor,
				Position = new Vector3(0f, 0.03f, halfLength),
				Emitting = false
			};
			AddChild(sternWakeParticles);
		}
	}

	/// <summary>
	/// 根据舰船的实时运动状态，开启或关闭船头和船尾粒子发射
	/// </summary>
	void UpdateNavalWaves(bool moving, float delta)
	{
		EnsureNavalWaveParticles();
		if (bowWaveParticles is not null && bowWaveParticles.Emitting != moving)
		{
			bowWaveParticles.Emitting = moving;
		}
		if (sternWakeParticles is not null && sternWakeParticles.Emitting != moving)
		{
			sternWakeParticles.Emitting = moving;
		}
	}

	/// <summary>
	/// 确保战机双发喷气发动机尾焰与高空热浪尾流粒子已被初始化
	/// </summary>
	void EnsureJetExhaustParticles()
	{
		if (jetExhaustParticles.Count > 0)
			return;

		if (UnitKey != "fighter" && visualKey != "fighter")
			return;

		var particleQuad = new QuadMesh { Size = new Vector2(0.18f, 0.18f) };

		// 左右双发喷气口本地坐标（对应 F-22 猛禽机尾双发隐身二维矢量排气喷口位置）
		var nozzles = new[]
		{
			new Vector3(-0.52f, -0.34f, 3.65f),
			new Vector3(0.52f, -0.34f, 3.65f)
		};

		var parentNode = visualRoot ?? this;

		// 生成纯净无边缘白边的径向高斯柔和渐变粒子纹理 (彻底消除 QuadMesh 直角白框瑕疵)
		var particleImg = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
		for (int py = 0; py < 32; py++)
		{
			for (int px = 0; px < 32; px++)
			{
				float nx = (px - 15.5f) / 15.5f;
				float ny = (py - 15.5f) / 15.5f;
				float d = Mathf.Sqrt(nx * nx + ny * ny);
				float alpha = d >= 1.0f ? 0f : Mathf.Pow(Mathf.Clamp(1.0f - d, 0f, 1f), 1.8f);
				particleImg.SetPixel(px, py, new Color(1f, 1f, 1f, alpha));
			}
		}
		var particleTex = ImageTexture.CreateFromImage(particleImg);

		foreach (var nozzlePos in nozzles)
		{
			// 1. 核心马赫环高温加力尾焰 (Afterburner Jet Flame)
			var flame = new CpuParticles3D
			{
				Name = "AfterburnerFlame",
				Amount = 24,
				Lifetime = 0.14f,
				Explosiveness = 0.85f,
				Direction = new Vector3(0f, 0f, 1f), // 向机尾后方喷射
				Spread = 6f,
				Gravity = Vector3.Zero,
				InitialVelocityMin = 8.0f,
				InitialVelocityMax = 14.0f,
				ScaleAmountMin = 0.25f,
				ScaleAmountMax = 0.48f,
				Position = nozzlePos,
				LocalCoords = true,
				Mesh = particleQuad
			};

			var flameMat = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = Colors.White,
				AlbedoTexture = particleTex,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				BlendMode = BaseMaterial3D.BlendModeEnum.Add,
				BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
				VertexColorUseAsAlbedo = true
			};
			flame.MaterialOverride = flameMat;

			var flameScale = new Curve();
			flameScale.AddPoint(new Vector2(0f, 0.6f));
			flameScale.AddPoint(new Vector2(0.4f, 1.2f));
			flameScale.AddPoint(new Vector2(1f, 0.1f));
			flame.ScaleAmountCurve = flameScale;

			// 电光青蓝核心 -> 炽烈暖橙红 -> 燃尽火光
			var flameRamp = CombatProjectile.CreateRamp4(
				new Color(1.2f, 2.2f, 2.8f, 1f), 0.25f,
				new Color(2.4f, 1.4f, 0.4f, 1f), 0.65f,
				new Color(1.8f, 0.4f, 0.08f, 0.9f),
				new Color(0.6f, 0.1f, 0.02f, 0f));
			flame.ColorRamp = flameRamp;

			parentNode.AddChild(flame);
			flame.Emitting = true;
			jetExhaustParticles.Add(flame);

			// 2. 超音速航迹云在近战空战视角下关闭粒子面片，保持机尾超音速加力喷口的纯净高光
			var smoke = new CpuParticles3D
			{
				Name = "JetSmokeTrail",
				Amount = 1,
				Lifetime = 0.1f,
				Position = nozzlePos,
				Emitting = false
			};
		}
	}

	/// <summary>
	/// 根据战斗机实时飞行状态更新喷气尾焰与尾流发射
	/// </summary>
	void UpdateJetExhaust(bool moving, float delta)
	{
		EnsureJetExhaustParticles();
		bool shouldEmit = moving && !parkedAtAirfield;
		foreach (var p in jetExhaustParticles)
		{
			if (GodotObject.IsInstanceValid(p) && p.Emitting != shouldEmit)
				p.Emitting = shouldEmit;
		}
	}

	/// <summary>
	/// 开启高空战斗机无缝大环线巡航航线
	/// </summary>
	public void StartAirPatrolCircuit()
	{
		if (!BattleUnitCatalog.IsAirUnit(UnitKey) || parkedAtAirfield)
			return;

		isAirPatrolCircuit = true;
		var center = GlobalPosition;
		float height = CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
		center.Y = height;
		float radius = 32f;
		int pointsCount = 8;
		var circuit = new List<Vector3>();

		for (int i = 0; i < pointsCount; i++)
		{
			float angle = (i / (float)pointsCount) * Mathf.Tau;
			var pt = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			circuit.Add(NormalizeCommandPosition(pt));
		}

		MoveAlongPath(circuit, false);
	}

	/// <summary>
	/// 固定翼战机到达单点目标后，自动顺切入高空 26~32 米大半径盘旋待机环线 (Loiter Orbit)，绝不在空中原地刹停或硬直悬停
	/// </summary>
	public void StartAirLoiterOrbit(Vector3 center)
	{
		if (!BattleUnitCatalog.IsFixedWingAircraft(UnitKey) || parkedAtAirfield || IsDockedOnCarrier)
			return;

		isAirPatrolCircuit = true;
		float height = CruiseHeight > 0.1f ? CruiseHeight : BattleUnitCatalog.SpawnHeight(UnitKey);
		center.Y = height;
		float radius = UnitKey == "bomber" ? 32f : 24f;
		int pointsCount = 8;
		var circuit = new List<Vector3>();

		// 依据战机当前相对目标点方位角，顺切入圆周航线，避免折线突跳
		var fromCenter = GlobalPosition - center;
		fromCenter.Y = 0f;
		float currentAngle = fromCenter.LengthSquared() > 0.01f ? Mathf.Atan2(fromCenter.Z, fromCenter.X) : 0f;

		for (int i = 1; i <= pointsCount; i++)
		{
			float angle = currentAngle + (i / (float)pointsCount) * Mathf.Tau;
			var pt = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
			circuit.Add(NormalizeCommandPosition(pt));
		}

		MoveAlongPath(circuit, false);
	}

	#region Carrier Mothership & Catapult Air Wing System
	public void InitializeCarrierAirWing(BattleGameManager manager)
	{
		if (carrierAirWing.Count > 0) return;

		string[] complement = { "fighter", "bomber", "fighter", "bomber" };
		for (int i = 0; i < complement.Length; i++)
		{
			SpawnSingleCarrierPlane(manager, complement[i], i);
		}
	}

	void SpawnCarrierReplacementAircraft(BattleGameManager manager)
	{
		var usedSlots = new HashSet<int>();
		int fighters = 0, bombers = 0;
		for (int i = 0; i < carrierAirWing.Count; i++)
		{
			var p = carrierAirWing[i];
			if (GodotObject.IsInstanceValid(p))
			{
				if (p.CarrierSlotIndex >= 0) usedSlots.Add(p.CarrierSlotIndex);
				if (p.UnitKey == "fighter") fighters++;
				else if (p.UnitKey == "bomber") bombers++;
			}
		}

		int freeSlot = 0;
		for (int s = 0; s < BattleUnitCatalog.CarrierSlotCount; s++)
		{
			if (!usedSlots.Contains(s))
			{
				freeSlot = s;
				break;
			}
		}

		string chosenType = (fighters <= bombers) ? "fighter" : "bomber";
		SpawnSingleCarrierPlane(manager, chosenType, freeSlot);
	}

	void SpawnSingleCarrierPlane(BattleGameManager manager, string planeKey, int slotIndex)
	{
		Vector3 slotOffset = BattleUnitCatalog.CarrierDeckSlots[slotIndex % BattleUnitCatalog.CarrierDeckSlots.Length];
		Vector3 spawnPos = GlobalPosition + GlobalTransform.Basis * slotOffset;
		var plane = manager.SpawnUnit(planeKey, spawnPos, PlayerOwned);
		if (plane is not null)
		{
			plane.CarrierHome = this;
			plane.DockOnCarrier(this, slotIndex);
			plane.CompleteCarrierServiceImmediately();
			carrierAirWing.Add(plane);
			AutoCombatArenaController.Instance?.RegisterUnit(plane, PlayerOwned);
		}
	}

	public bool TryScrambleCarrierPlane(Node3D target)
	{
		bool targetIsAir = target is RtsUnit u && BattleUnitCatalog.IsAirUnit(u.UnitKey);

		RtsUnit? chosen = null;
		if (targetIsAir)
		{
			chosen = carrierAirWing.FirstOrDefault(p => p.IsDockedOnCarrier && !p.IsCarrierServicing && p.UnitKey == "fighter")
				  ?? carrierAirWing.FirstOrDefault(p => p.IsDockedOnCarrier && !p.IsCarrierServicing);
		}
		else
		{
			chosen = carrierAirWing.FirstOrDefault(p => p.IsDockedOnCarrier && !p.IsCarrierServicing && p.UnitKey == "bomber")
				  ?? carrierAirWing.FirstOrDefault(p => p.IsDockedOnCarrier && !p.IsCarrierServicing);
		}

		if (chosen is not null)
		{
			chosen.LaunchFromCarrier(target);
			return true;
		}
		return false;
	}

	public int FindFreeAirWingSlot()
	{
		var used = new HashSet<int>();
		for (int i = 0; i < carrierAirWing.Count; i++)
		{
			var p = carrierAirWing[i];
			if (GodotObject.IsInstanceValid(p) && p.IsDockedOnCarrier && p.CarrierSlotIndex >= 0)
			{
				used.Add(p.CarrierSlotIndex);
			}
		}
		for (int s = 0; s < BattleUnitCatalog.CarrierSlotCount; s++)
		{
			if (!used.Contains(s)) return s;
		}
		return -1;
	}

	public void DockOnCarrier(RtsUnit carrier, int slotIndex)
	{
		DockedCarrier = carrier;
		CarrierHome = carrier;
		CarrierSlotIndex = slotIndex;
		IsReturningToCarrier = false;
		isCatapultLaunching = false;
		catapultProgress = 0f;
		parkedAtAirfield = false;
		parkingAtAirfield = false;
		AttackTarget = null;
		AttackMoving = false;
		Velocity = Vector3.Zero;
		Vector3 slotOffset = BattleUnitCatalog.CarrierDeckSlots[slotIndex % BattleUnitCatalog.CarrierDeckSlots.Length];
		GlobalPosition = carrier.GlobalPosition + carrier.GlobalTransform.Basis * slotOffset;
		GlobalRotation = carrier.GlobalRotation;

		GetNodeOrNull<Node3D>("AirShadow")?.SetVisible(false);
		GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.SetDisabled(true);

		// 开始 10 秒停靠整备（加注燃油、补充弹药、修复机体损伤）
		StartCarrierService();
	}

	public void StartCarrierService(bool force10Seconds = true)
	{
		bool needsService = force10Seconds 
			|| (MaxHealth > 0 && Health < MaxHealth * 0.99f)
			|| (maxFuelSeconds > 0 && CurrentFuel < maxFuelSeconds * 0.99f)
			|| (MaxAmmo > 0 && CurrentAmmo < MaxAmmo);

		if (!needsService)
		{
			IsCarrierServicing = false;
			CarrierServiceTimeRemaining = 0f;
			return;
		}

		IsCarrierServicing = true;
		CarrierServiceTimeRemaining = BattleUnitCatalog.CarrierTurnaroundSeconds;
		carrierServiceStartHealth = Health;
		carrierServiceStartFuel = CurrentFuel;
		carrierServiceStartAmmo = CurrentAmmo;
		carrierServiceFxTimer = 0f;

		if (PlayerOwned)
		{
			FindHud()?.ShowAlert($"{DisplayName} 已停靠航母，开始 10 秒补给整备（燃油、弹药、机体修复）");
		}
	}

	public void CompleteCarrierServiceImmediately()
	{
		IsCarrierServicing = false;
		CarrierServiceTimeRemaining = 0f;
		Health = MaxHealth;
		CurrentFuel = maxFuelSeconds;
		CurrentAmmo = MaxAmmo > 0 ? MaxAmmo : 0;
	}

	public void ReplenishAirSupply()
	{
		if (!BattleUnitCatalog.IsAirUnit(UnitKey) || IsDead)
			return;

		CurrentFuel = maxFuelSeconds;
		warnedLowFuel = false;
		warnedNoAirfield = false;

		if (MaxAmmo > 0)
		{
			CurrentAmmo = MaxAmmo;
		}

		if (IsCarrierServicing)
		{
			CompleteCarrierServiceImmediately();
		}

		SpawnCarrierServiceSparks();
	}

	void SpawnCarrierServiceSparks()
	{
		var sparks = new CpuParticles3D
		{
			Name = "CarrierServiceSparksFx",
			Amount = 14,
			Lifetime = 0.45f,
			OneShot = true,
			Explosiveness = 0.90f,
			Direction = Vector3.Up + new Vector3((float)GD.RandRange(-0.4, 0.4), 0f, (float)GD.RandRange(-0.4, 0.4)),
			Spread = 45f,
			Gravity = new Vector3(0f, -4.0f, 0f),
			InitialVelocityMin = 2.0f,
			InitialVelocityMax = 4.2f,
			ScaleAmountMin = 0.15f,
			ScaleAmountMax = 0.40f,
			Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f },
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(1.0f, 0.85f, 0.35f, 0.95f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha
			}
		};
		var parent = GetParent() ?? this;
		parent.AddChild(sparks);
		sparks.GlobalPosition = GlobalPosition + Vector3.Up * 0.25f;
		sparks.Emitting = true;
		sparks.GetTree().CreateTimer(0.8).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(sparks)) sparks.QueueFree();
		};
	}

	public void LaunchFromCarrier(Node3D? target = null)
	{
		if (DockedCarrier is null || !GodotObject.IsInstanceValid(DockedCarrier))
			return;

		var carrier = DockedCarrier;
		isCatapultLaunching = true;
		catapultProgress = 0f;
		catapultTargetAfterLaunch = target;
		catapultStartPos = carrier.GlobalPosition + carrier.GlobalTransform.Basis * new Vector3(-0.45f, 0.42f, 0.2f);
		catapultEndPos = carrier.GlobalPosition + carrier.GlobalTransform.Basis * new Vector3(-0.45f, 0.52f, -4.5f);

		carrier.PlayCatapultLaunchEffects(catapultStartPos, -carrier.GlobalTransform.Basis.Z);
	}

	public void PlayCatapultLaunchEffects(Vector3 bowPos, Vector3 forwardDir)
	{
		BattleFeedback.WeaponFire(this, bowPos, "catapult", false);
		if (EffectPool.Instance is not null)
		{
			EffectPool.Instance.SpawnCatapultSteam(bowPos, forwardDir);
			return;
		}
	}

	void ProcessDockedOnCarrierPhysics(float dt)
	{
		if (!GodotObject.IsInstanceValid(DockedCarrier) || DockedCarrier.IsDead)
		{
			DockedCarrier = null;
			CarrierSlotIndex = -1;
			IsCarrierServicing = false;
			isCatapultLaunching = false;
			Velocity = -GlobalTransform.Basis.Z * MoveSpeed;
			GetNodeOrNull<Node3D>("AirShadow")?.SetVisible(true);
			GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.SetDisabled(false);
			GlobalPosition += Velocity * dt;
			MaintainMovementLayer();
			return;
		}

		if (isCatapultLaunching)
		{
			catapultProgress += dt * 2.4f;
			GlobalPosition = catapultStartPos.Lerp(catapultEndPos, Mathf.Clamp(catapultProgress, 0f, 1f));
			GlobalRotation = DockedCarrier.GlobalRotation;

			if (catapultProgress >= 1f)
			{
				isCatapultLaunching = false;
				DockedCarrier = null;
				CarrierSlotIndex = -1;
				IsCarrierServicing = false;
				parkedAtAirfield = false;
				parkingAtAirfield = false;

				GetNodeOrNull<Node3D>("AirShadow")?.SetVisible(true);
				GetNodeOrNull<CollisionShape3D>("CollisionShape3D")?.SetDisabled(false);

				Velocity = -GlobalTransform.Basis.Z * (MoveSpeed * 1.5f);
				TargetPosition = GlobalPosition - GlobalTransform.Basis.Z * 35f + Vector3.Up * (CruiseHeight - GlobalPosition.Y);

				if (catapultTargetAfterLaunch is not null && GodotObject.IsInstanceValid(catapultTargetAfterLaunch))
				{
					Attack(catapultTargetAfterLaunch);
				}
			}
			return;
		}

		// 航母甲板 10 秒停靠整备处理 (10s Turnaround Maintenance)
		if (IsCarrierServicing)
		{
			CarrierServiceTimeRemaining = Mathf.Max(0f, CarrierServiceTimeRemaining - dt);
			float progress = 1.0f - Mathf.Clamp(CarrierServiceTimeRemaining / BattleUnitCatalog.CarrierTurnaroundSeconds, 0f, 1f);

			// 1. 恢复飞机损坏 (Repair Damage)
			if (MaxHealth > carrierServiceStartHealth)
			{
				Health = Mathf.Min(MaxHealth, carrierServiceStartHealth + (MaxHealth - carrierServiceStartHealth) * progress);
			}

			// 2. 提供油量补给 (Refuel)
			if (maxFuelSeconds > carrierServiceStartFuel)
			{
				CurrentFuel = Mathf.Min(maxFuelSeconds, carrierServiceStartFuel + (maxFuelSeconds - carrierServiceStartFuel) * progress);
			}

			// 3. 提供弹药补给 (Rearm Ammo)
			if (MaxAmmo > carrierServiceStartAmmo)
			{
				CurrentAmmo = Mathf.Clamp(carrierServiceStartAmmo + Mathf.FloorToInt((MaxAmmo - carrierServiceStartAmmo) * progress), 0, MaxAmmo);
			}

			// 4. 地勤维修火花与补给微粒
			carrierServiceFxTimer += dt;
			if (carrierServiceFxTimer >= 0.70f)
			{
				carrierServiceFxTimer = 0f;
				SpawnCarrierServiceSparks();
			}

			if (CarrierServiceTimeRemaining <= 0f)
			{
				IsCarrierServicing = false;
				Health = MaxHealth;
				CurrentFuel = maxFuelSeconds;
				CurrentAmmo = MaxAmmo > 0 ? MaxAmmo : 0;
				warnedLowFuel = false;
				warnedNoAirfield = false;

				if (PlayerOwned)
				{
					FindHud()?.ShowAlert($"{DisplayName} 航母整备完毕：燃油满、弹药满、机体已完全修复！");
				}
			}
		}

		Vector3 slotOffset = BattleUnitCatalog.CarrierDeckSlots[CarrierSlotIndex % BattleUnitCatalog.CarrierDeckSlots.Length];
		GlobalPosition = DockedCarrier.GlobalPosition + DockedCarrier.GlobalTransform.Basis * slotOffset;
		GlobalRotation = DockedCarrier.GlobalRotation;
		Velocity = Vector3.Zero;
	}

	void ProcessCarrierLogic(float dt)
	{
		if (!carrierAirWingInitialized && BattleGameManager.Instance is { } mgr)
		{
			carrierAirWingInitialized = true;
			InitializeCarrierAirWing(mgr);
		}

		for (int i = carrierAirWing.Count - 1; i >= 0; i--)
		{
			var p = carrierAirWing[i];
			if (!GodotObject.IsInstanceValid(p) || p.IsDead)
			{
				carrierAirWing.RemoveAt(i);
			}
		}

		if (carrierAirWing.Count < BattleUnitCatalog.CarrierSlotCount && BattleGameManager.Instance is { } gameMgr)
		{
			carrierReplenishTimer -= dt;
			if (carrierReplenishTimer <= 0f)
			{
				carrierReplenishTimer = 10.0f;
				SpawnCarrierReplacementAircraft(gameMgr);
			}
		}

		if (AttackTarget is not null && GodotObject.IsInstanceValid(AttackTarget) && IsLiveEnemyTarget(AttackTarget))
		{
			float dist = GlobalPosition.DistanceTo(AttackTarget.GlobalPosition);
			carrierScrambleTimer -= dt;
			if (carrierScrambleTimer <= 0f && dist <= BattleUnitCatalog.CarrierAirStrikeRange)
			{
				if (TryScrambleCarrierPlane(AttackTarget))
				{
					carrierScrambleTimer = BattleUnitCatalog.CarrierScrambleInterval;
				}
			}
		}
	}

	public void ReturnToCarrierBase()
	{
		if (CarrierHome is null || !GodotObject.IsInstanceValid(CarrierHome) || CarrierHome.IsDead)
		{
			IsReturningToCarrier = false;
			return;
		}

		IsReturningToCarrier = true;
		AttackTarget = null;
		AttackMoving = false;

		Vector3 approachPos = CarrierHome.GlobalPosition + CarrierHome.GlobalTransform.Basis * new Vector3(0f, 0.45f, 4.5f);
		TargetPosition = approachPos;

		float distToCarrier = GlobalPosition.DistanceTo(CarrierHome.GlobalPosition);
		if (distToCarrier <= 5.8f)
		{
			int slot = CarrierHome.FindFreeAirWingSlot();
			if (slot >= 0)
			{
				DockOnCarrier(CarrierHome, slot);
			}
		}
	}
	#endregion
}

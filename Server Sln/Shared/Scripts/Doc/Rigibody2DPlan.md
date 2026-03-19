Scope / Goal

Implement a simple physics layer in SharedLibrary (Unity client authoritative) so:





Add a new EntityComponent like Rigidbody2D for both Puck and Paddle with fields Vel, mass, max speed.



Puck moves each tick based on Vel and responds to collisions with Wall and Paddle.



Paddle movement is not affected by collisions (it stays controlled by Match.MovePaddle).

How it fits the current architecture





Current simulation model stores state in MH.Core.Entity components; visuals just follow MH.Core.Root2D.Position.



Current collision detection exists as pure math components: CircleCollider/RectCollider trigger OnCollision when overlapping, and BaseCollider.Tick() checks against a list of TrackOthers.



Right now Match.Tick(deltaTime) only ticks the puck, so we will expand ticking to paddles as well (so paddle velocity is available for the collision formula).

flowchart LR
  Input[Mouse/Network Input] --> Match[MH.GameLogic.Match]
  Match --> Entities[Entity objects: Paddle/Puck/Wall]
  Entities --> Step[Physics step in new Rigidbody2D-like component]
  Step --> Root[MH.Core.Root2D.Position updates]
  Root --> Views[EntityView2D updates Unity Transform]

Implementation Outline





Create a Rigidbody2D-like component





Add a new EntityComponent in Assets/SharedLibrary (and mirror to Server Sln/Shared for later parity):





Rigidbody2DComponent (name can vary) with public fields:





CustomVector2 Vel



float mass



float maxSpeed



Also include physics coefficients needed by the doc:





float bounciness (elasticity e)



float influence (paddle influence f)



float minSpeed



Add a mode flag:





bool isKinematic for paddles (position is driven externally; collisions do not alter it)



Tick behavior:





Puck (dynamic): integrate position Position += Vel * deltaTime and clamp/enforce min speed.



Paddle (kinematic): do not integrate position; instead compute paddle Vel from the change in Root2D.Position since last tick, so collision response can use V_paddle.



Implement puck collision response (custom colliders backend)





Use the existing overlap detection (CircleCollider vs CircleCollider/RectCollider) and hook puck collision handling via:





puckCircleCollider.OnCollision = HandlePuckCollision



Puck collision loop:





Gather collision targets once in Match:





all Wall colliders



all Paddle colliders



Provide them to the puck rigidbody as TrackOthers (or keep a separate list and call collision checks manually).



Collision response rules based on Assets/SharedLibrary/Doc/CollisionPhysicDoc.md:





For Puck vs Paddle (circle-circle):





n = normalize(P_puck_old - P_paddle_center)



V_rel = V_puck_old - V_paddle



V_reflect = Reflect(V_rel, n)



V_puck_new = (V_reflect * e) + (V_paddle * f)



clamp to maxSpeed, enforce minSpeed (never fully stop)



resolve penetration by pushing puck out along n (prevents immediate re-triggering)



For Puck vs Wall (circle-rect):





treat the wall as no “paddle influence” (use f = 0 or ignore paddle velocity)



compute a reasonable normal from closest point on the rect to puck center (or wall center approximation, but closest-point is better)



reflect puck velocity using bounciness



clamp + min speed + penetration resolution



Expand Match.Tick so paddle velocity is available





Update Assets/SharedLibrary/GameLogic/Match.cs:





tick each player’s Paddle rigidbody first (kinematic velocity update)



then tick _puck rigidbody (integration + collisions)



Keep wall ticking optional (only puck needs to collide; wall objects are static in this MVP).



Wire Match.Tick into Unity update loop





Update _MH/Scripts/GameRunner.cs (or add a dedicated MatchSimulationRunner) to call:





_currentMatch.Tick(Time.deltaTime) each frame (or Time.fixedDeltaTime in FixedUpdate).



This is required for puck movement to actually happen.



Add basic tunneling protection





Inside puck rigidbody tick, add sub-stepping:





compute subSteps from speed, deltaTime, and puck radius



repeat: integrate -> collision check -> resolve



This substitutes for Unity’s CollisionDetectionMode2D.Continuous since we are using custom colliders.



Mirror to server shared code (recommended for later)





Apply the same component and match logic to:





Server Sln/Shared/Scripts/*



Even if you keep simulation client-only today, keeping server parity avoids later merge pain.

Verification checklist





Start scene and spawn a match.



Move paddle with mouse.



Confirm:





puck bounces off walls



puck bounces off paddle using the formula (e and f)



paddle does not change speed/trajectory due to collisions



puck speed respects maxSpeed and never fully stops (min speed behavior)

Notes/Assumptions





Collision normal calculation uses geometric approximations from the colliders’ shapes because the current collider system only flags overlap.



The normal quality is good enough for MVP; later you can improve penetration/contact-point accuracy.


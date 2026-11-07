# Slot Machine Simulation

A classic 3-reel mechanical slot machine game built in Unity (Unity 6 / C#). Features procedural reel spinning, authentic deceleration and spring recoil physics, mathematical ratchet audio, win celebrations, and bonus rounds.

- **GitHub Repository**: [https://github.com/ShivanshuK12399/Slot-Machine](https://github.com/ShivanshuK12399/Slot-Machine)
- **Play Online (WebGL)**: [https://shivanshu-kushwaha.itch.io/slot-machine-simulation](https://shivanshu-kushwaha.itch.io/slot-machine-simulation)

---

## How to Play (WebGL / Desktop)

You can play directly in your browser on itch.io:
👉 **[Play on Itch.io](https://shivanshu-kushwaha.itch.io/slot-machine-simulation)**

### Controls:
- **Spin**: Drag the cabinet lever down with your mouse and release, or simply hit **Spacebar**.
- **Adjust Bet**: Use the **+** / **-** buttons on the cabinet (or **Up / Down Arrow** keys).
- **Max Bet**: Click the **MAX** button (or press **M**).
- **Claim Winnings**: When the win modal pops up, click **CLAIM** (or wait 4 seconds for it to bank automatically).

If your bet is higher than your current balance, the machine prompts you to lower your wager. If your balance hits zero, the machine gives you a heads-up and reloads fresh credits after 3 seconds.

---

## Payouts & Bonus Features

The game uses 4 symbol types with clear win conditions:

| Symbol | Payout | Description |
| :--- | :---: | :--- |
| **Symbol 1** | **5x** | BIG WIN! |
| **Symbol 2** | **10x** | SUPER WIN! |
| **Symbol 3** | **20x** | MEGA WIN! |
| **Symbol 4** | **50x** | JACKPOT! (Wild & Bonus trigger) |

### Bonus Mechanics:
1. **Wild Substitution**:
   - Symbol 4 acts as a Wild symbol. If two matching symbols land with a Symbol 4 (e.g. `[Symbol 2, Symbol 2, Wild]`), it counts as a full 3-of-a-kind match and triggers a **WILD WIN!**.
2. **Free Spins (Bonus Round)**:
   - Hitting 3x Symbol 4 awards the 50x Jackpot plus **5 Free Spins**.
   - During Free Spins, spins cost **0 credits** and all wins are doubled (**2x multiplier**).
3. **Win Celebration Modal**:
   - Clean pop-up dialog with juicy scale animation, displaying the win amount and a claim button.
4. **Win Probability Slider**:
   - Exposed directly in the Inspector on `SlotMachineController` (`0.0` to `1.0`), letting designers set the exact win rate for balancing or testing.

---

## Thought Process & Approach

### 1. Mechanical Feel Over Generic Animations
Most digital slot games use simple linear looping textures. Here, each reel is treated like a real mechanical drum:
- It spins up with an initial ramp-up.
- Reels stop one by one (Reel 1 at 1.0s, Reel 2 at 1.8s, Reel 3 at 2.6s) to build anticipation.
- When stopping, each reel decelerates along a quadratic braking curve and settles with a subtle mechanical spring recoil (a small damped 4px oscillation), mimicking a physical pawl locking into a star-wheel.

### 2. Mathematical Audio (Zero Audio Assets)
Instead of looping a static `.wav` file, all mechanical audio is generated procedurally in code:
- As the reel rotates, each physical tooth passing fires a synthesized micro-click.
- The click pitch remains constant at a solid 1.0x mechanical tone, but the click tempo naturally follows reel speed: rapid chatter at peak spin, gradually spacing out into individual distinct ticks as the reel brakes to a stop.
- No external audio clips needed, no voice-stealing bugs, and zero garbage collection in update loops.

### 3. Architecture & Clean Code
- **Model-View-Controller**: Game state, balance, and RNG outcome calculations live in pure logic scripts (`SlotMachineController`, `PayoutOutcomeService`), decoupled from UI rendering (`SlotMachineView`, `WinCelebrationView`).
- **Zero GC in Hot Paths**: All frame updates, distance wrapping, and reel recycling loop over pre-allocated arrays with zero heap allocations.
- **Fail-safe UI Lifecycles**: Popups and UI states are handled through `CanvasGroup` visibility rather than deactivating parent GameObjects, keeping event subscriptions and coroutines alive.


# Networked Shared-Control Platformer Prototype

A 2D platformer prototype built in Unity exploring a cooperative mechanic where two players control different aspects of the same character.

**Current status:** Multiplayer currently works over LAN. I am currently in the process of implementing a custom deterministic 2D physics engine.

## Controls (currently implemented)
#### Player 1:
* Horizontal movement (`A`, `D`)
* Double jump (`W`)
#### Player 2:
* Jump OR triple jump (`W`)
* Stop movement when pressing the opposite direction, otherwise dash (`A`, `D`)
* Dive if Player 1 is holding `S` (`S`)

The two players can swap controls by pressing `0` at the same time. This is planned to be replaced with a pause menu option.

## Demo
https://github.com/user-attachments/assets/73c99bcd-9922-4b66-bae8-9fb0df4e2522

## Dependencies
* Unity 2022.3.62f3
* Unity Netcode for GameObjects 1.12.2
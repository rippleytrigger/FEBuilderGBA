
.thumb

.include "../Common Definitions.inc"

MMBDrawHPStatusNoCombination:

	.global	MMBDrawHPStatusNoCombination
	.type	MMBDrawHPStatusNoCombination, %function

	.set MMBHPStatusXCoordinate,	EALiterals + 0
	.set MMBHPStatusYCoordinate,	EALiterals + 4
	.set MMBHeight,					EALiterals + 8

	@ Inputs:
	@ r0: pointer to proc state
	@ r1: pointer to unit in RAM

	push	{r4-r7, r14}

	mov		r4, r0
	mov		r5, r1

	@ Get tilemap position

	ldr		r0, MMBHPStatusXCoordinate
	ldr		r1, MMBHPStatusYCoordinate

	ldr		r2, =WindowBuffer

	lsl		r0, r0, #0x01
	lsl		r1, r1, #0x06

	add		r0, r1, r0
	add		r0, r0, r2

	ldr		r1, =0x2120
	strh	r1, [r0]
	add		r1, #0x01
	strh	r1, [r0, #0x02]
	mov		r1, #0x00
	strh	r1, [r0, #0x04]
	strh	r1, [r0, #0x06]
	strh	r1, [r0, #0x0A]
	strh	r1, [r0, #0x0C]
	ldr		r1, =0x213E
	strh	r1, [r0, #0x08]

	@ Check if we're on a unit to avoid
	@ bad number drawing

	mov		r0, r4
	add		r0, #UnitFlag

	ldrb	r0, [r0]
	cmp		r0, #0x00
	bne		End

	mov		r0, r5
	cmp		r0, #0x00
	beq		End

	@ Get positions for numbers

	ldr		r6, MMBHPStatusXCoordinate
	ldr		r7, MMBHPStatusYCoordinate

	lsl		r6, r6, #0x03 @ mult by 8
	lsl		r7, r7, #0x03

	add		r6, #24 @ past the HP label

	@ check for lower window

	mov		r0, r4
	add		r0, #WindowPosType
	ldrb	r0, [r0]
	lsl		r0, r0, #0x03
	ldr		r1, =WindowSideTable
	add		r0, r1, r0
	mov		r1, #0x03
	ldsb	r0, [r0, r1] @ -1 top 1 bottom
	cmp		r0, #0x00
	blt		SkipBottom

	ldr		r0, MMBHeight
	mov		r1, #20
	sub		r1, r1, r0

	lsl		r1, r1, #0x03
	add		r7, r7, r1

SkipBottom:

	@ Get HP numbers

	mov		r0, r5
	ldr		r1, =GetCurrentHP
	mov		r14, r1
	.short 0xF800

	cmp		r0, #99
	ble		SkipDashedCurrentHP

	@ HP too high, display --

	mov		r0, #0xFF

SkipDashedCurrentHP:

	mov		r2, r0
	mov		r0, r6
	mov		r1, r7

	ldr		r3, =MMBDrawUnsignedNumber
	mov		r14, r3

	.short 0xF800

	add		r6, r6, #25

	mov		r0, r5
	ldr		r1, =GetMaxHP
	mov		r14, r1
	.short 0xF800

	cmp		r0, #99
	ble		SkipDashedMaxHP

	@ HP too high, display --

	mov		r0, #0xFF

SkipDashedMaxHP:

	mov		r2, r0
	mov		r0, r6
	mov		r1, r7

	ldr		r3, =MMBDrawUnsignedNumber
	mov		r14, r3

	.short 0xF800

End:

	pop		{r4-r7}
	pop		{r0}
	bx		r0

.ltorg

EALiterals:
	@ MMBHPStatusXCoordinate
	@ MMBHPStatusYCoordinate
	@ MMBHeight








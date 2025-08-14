package primitives

import (
	"fmt"
	"math/bits"
	"strings"
)

// CharSet efficiently represents a set of characters using bit manipulation.
// It supports characters from '`' (96) to 'z' (122), total of 27 characters.
// This fits perfectly in a uint32.
type CharSet struct {
	bits uint32
}

const (
	minChar  = '`'                   // 96
	maxChar  = 'z'                   // 122
	numChars = maxChar - minChar + 1 // 27 characters
	fullBits = 0x7ffffff
)

// NewCharSet creates a new optimized character set.
func NewCharSet() *CharSet {
	return &CharSet{}
}

// DefaultCharSet creates the default character set for the generator.
func DefaultCharSet() *CharSet {
	return &CharSet{}
}

// Add adds a character to the set.
func (c *CharSet) Add(r rune) error {
	if r < minChar || r > maxChar {
		return fmt.Errorf("character %c is out of range", r)
	}

	bitPos := uint(r - minChar)
	c.bits |= 1 << bitPos
	return nil
}

// AddAll adds all characters from another set to this set.
func (c *CharSet) AddAll(other *CharSet) {
	c.bits |= other.bits
}

// Contains checks if a character is in the set.
func (c *CharSet) Contains(r rune) bool {
	if r < minChar || r > maxChar {
		return false
	}
	bitPos := uint(r - minChar)
	return c.bits&(1<<bitPos) != 0
}

// IsFull checks if the set is full.
func (c CharSet) IsFull() bool {
	// return c.Count() == numChars
	return c.bits == fullBits
}

// Capacity returns the number of characters that can be added to the set.
func (c *CharSet) Capacity() int {
	return numChars
}

// Count returns the number of characters in the set.
func (c CharSet) Count() int {
	return bits.OnesCount32(c.bits)
}

// Clear removes all characters from the set.
func (c *CharSet) Clear() {
	c.bits = 0
}

// Clone creates a copy of the character set.
func (c *CharSet) Clone() *CharSet {
	return &CharSet{
		bits: c.bits,
	}
}

// ContainsAll checks if the set contains all characters in another set.
func (c *CharSet) ContainsAll(other *CharSet) bool {
	return c.bits&other.bits == other.bits
}

// Intersect performs an intersection with another set.
func (c *CharSet) Intersect(other *CharSet) {
	c.bits &= other.bits
}

// Intersects checks if the set intersects with another set.
func (c CharSet) Intersects(other CharSet) bool {
	return c.bits&other.bits != 0
}

// String returns a string representation of the set.
func (c *CharSet) String() string {
	if c.bits == 0 {
		return "available [] (0/27)"
	}

	var chars []string
	for i := range uint(numChars) {
		if c.bits&(1<<i) != 0 {
			chars = append(chars, fmt.Sprintf("'%c'", rune(minChar+i)))
		}
	}
	return fmt.Sprintf("available [%s] (%d/%d)", strings.Join(chars, ", "), c.Count(), numChars)
}

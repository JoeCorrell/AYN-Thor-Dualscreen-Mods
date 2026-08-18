package com.wemu.dualscreen

import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Color
import com.thor.core.designsystem.theme.LocalThorTheme
import com.thor.core.designsystem.theme.ThorColors
import com.thor.core.designsystem.theme.ThorTheme

/**
 * The standalone app's own palette, stated rather than inherited.
 *
 * <h2>Why it has one at all</h2>
 *
 * The pages this app draws take every colour from the theme above them. Inside
 * wemu that is the theme the player chose, so they look like the launcher they
 * are part of. This app cannot read that choice — it lives in wemu's private
 * storage — and what it fell back to was the design system's default, which is
 * Material's baseline palette. That is where the blue came from, and patching
 * the two or three colours that showed it most only moved the problem: fix the
 * text and the accent stays blue, fix the accent and a surface underneath is
 * still wrong, and every round leaves a page looking like two themes arguing.
 *
 * So this states all of it. Every colour the design system asks for is given a
 * value here, which means nothing is inherited and nothing can surprise it if
 * the default palette ever changes.
 *
 * <h2>Why these colours</h2>
 *
 * Paper and ink. The panels on the game pages are the game's own wood and
 * parchment, and chrome around them has to be quieter than they are, so the
 * background is a warm off-white and text is near-black rather than pure black —
 * pure black against a warm ground reads as a hole. The accent is amber, which
 * is the one hue in this range that can mark something as urgent without
 * shouting over the art beside it.
 *
 * <h2>What it keeps from the design system</h2>
 *
 * Everything that is not colour: spacing, shapes, motion, surfaces and the type
 * scale still come from `ThorTheme`, so a page laid out for the console is laid
 * out identically here. Only the palette is replaced.
 */
@Composable
fun DualScreenTheme(content: @Composable () -> Unit) {
    ThorTheme {
        val base = LocalThorTheme.current
        val themed = remember(base) { base.copy(colors = Paper) }

        CompositionLocalProvider(
            LocalThorTheme provides themed,
            LocalContentColor provides Paper.onSurface,
        ) {
            MaterialTheme(
                colorScheme = MaterialTheme.colorScheme.copy(
                    primary = Paper.primary,
                    onPrimary = Color.White,
                    primaryContainer = Paper.surfaceHighest,
                    onPrimaryContainer = Paper.onSurface,
                    secondary = Paper.secondary,
                    onSecondary = Color.White,
                    tertiary = Paper.primary,
                    onTertiary = Color.White,
                    background = Paper.background,
                    onBackground = Paper.onBackground,
                    surface = Paper.surface,
                    onSurface = Paper.onSurface,
                    surfaceVariant = Paper.surfaceHighest,
                    onSurfaceVariant = Paper.onSurfaceVariant,
                    surfaceContainer = Paper.surfaceElevated,
                    surfaceContainerHigh = Paper.surfaceHighest,
                    surfaceContainerHighest = Paper.surfaceHighest,
                    outline = Paper.outline,
                    outlineVariant = Paper.outline,
                    error = Paper.error,
                    onError = Color.White,
                    scrim = Paper.scrim,
                ),
                typography = MaterialTheme.typography,
                shapes = MaterialTheme.shapes,
                content = content,
            )
        }
    }
}

private val Ink = Color(0xFF1B1714)
private val InkMuted = Color(0xFF5C5044)
private val Parchment = Color(0xFFF6EEDD)
private val Card = Color(0xFFFFFAF0)
private val CardHigh = Color(0xFFEADDC4)
private val Edge = Color(0xFFC9B79A)
private val Amber = Color(0xFFC8791E)
private val AmberDeep = Color(0xFF8A4E12)

private val Paper = ThorColors(
    primary = Amber,
    secondary = AmberDeep,
    accentEnd = AmberDeep,
    background = Parchment,
    surface = Card,
    surfaceElevated = Card,
    surfaceHighest = CardHigh,
    onBackground = Ink,
    onSurface = Ink,
    onSurfaceVariant = InkMuted,
    cursor = Amber,
    glow = Color(0xFFE0A64A),
    outline = Edge,
    error = Color(0xFFA33223),
    scrim = Color(0x99000000),
    tints = listOf(Amber, AmberDeep, Color(0xFF6B8E4E), Color(0xFF4E7A8E), Color(0xFF8E4E6B)),
)

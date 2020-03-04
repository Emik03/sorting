using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using Newtonsoft.Json;
using KModkit;
using Assets;

public class Sorting : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Info;
    public TextMesh Screen;
    public Component background;
    public KMSelectable[] btn;
    public TextMesh[] txt;
    public Transform[] pos;

    internal Algorithms algorithms = new Algorithms();

    private Color32 _buttonColor = new Color32(96, 176, 176, 255),
                    _blinkingColor = new Color32(144, 255, 255, 255),
                    _highlightColor = new Color32(255, 255, 255, 255),
                    _backgroundColor = new Color32(48, 128, 144, 255),
                    _strikeColor = new Color32(255, 128, 128, 255),
                    _strikeBackgroundColor = new Color32(128, 64, 64, 255);

    private bool _isSolved = false, _lightsOn = false, _buttonDelay = false, _bogoSort = false;
    private byte _frames = 0, _swapButtons = 0, _swapIndex = 1, _pushTimes = 0;
    private int _moduleId = 0;
    private float _swapAnimated = 0;
    private string _currentAlgorithm = "";

    private List<byte> _selected = new List<byte>();
    private byte[] _buttons = new byte[5], _initialButtons = new byte[5];
    private readonly string[] _algorithms = new string[12]
    {
        "BUBBLE", "SELECTION", "INSERTION", "RADIX", "MERGE", "COMB", "HEAP", "COCKTAIL", "ODDEVEN", "CYCLE", "FIVE", "QUICK"
    };

    private static bool _playSound = true;
    private static int _moduleIdCounter = 1;

    /// <summary>
    /// Animation for clearing the module.
    /// </summary>
    private void FixedUpdate()
    {
        if (_swapAnimated > 0)
        {
            //buttons move towards positions
            for (int i = 0; i < pos.Length; i++)
            {
                byte division = 1;

                //gets the right division for the ease
                switch (Mathf.Abs(_selected[1] - _selected[0]))
                {
                    case 1:
                        division = 111;
                        break;

                    case 2:
                        division = 120;
                        break;

                    case 3:
                        division = 67;
                        break;

                    case 4:
                        division = 60;
                        break;
                }

                //swap button placements
                btn[_selected[i]].transform.position = Vector3.MoveTowards(btn[_selected[i]].transform.position, pos[pos.Length - 1 - i].position, Mathf.Pow(_swapAnimated, 3) / division);
            }

            //commit an actual swap
            if (_swapAnimated < 0.05f)
            {
                for (int i = 0; i < pos.Length; i++)
                {
                    //swapping positions
                    btn[_selected[i]].transform.position = pos[i].position;
                }

                ResetButtons();
            }

            _swapAnimated -= 0.05f;
        }

        //force question marks
        if (_bogoSort)
        {
            //makes all numbers question marks
            for (int i = 0; i < btn.Length; i++)
            {
                txt[i].text = "??";
            }
        }

        //if solved, cycle the flashing towards the left side
        if (_isSolved)
            _frames--;

        //if unsolved, cycle the flashing towards the right side
        else
            _frames++;

        //fixes modulo
        _frames += 100;
        _frames %= 100;

        //fun effect for clearing it, flashes colors back and forth
        for (int i = 0; i < btn.Length; i++)
        {
            if (btn[i].GetComponent<MeshRenderer>().material.color != _highlightColor && btn[i].GetComponent<MeshRenderer>().material.color != _strikeColor)
            {
                if (_frames >= i * 10 && _frames <= (i + 3) * 10)
                    btn[i].GetComponent<MeshRenderer>().material.color = _blinkingColor;

                else
                    btn[i].GetComponent<MeshRenderer>().material.color = _buttonColor;
            }
        }
    }

    /// <summary>
    /// Code that runs when bomb is loading.
    /// </summary>
    private void Start()
    {
        Module.OnActivate += Activate;
        _moduleId = _moduleIdCounter++;
    }

    /// <summary>
    /// Lights get turned on.
    /// </summary>
    void Activate()
    {
        Init();
        _lightsOn = true;
    }

    /// <summary>
    /// Initalising buttons.
    /// </summary>
    private void Awake()
    {
        for (int i = 0; i < 5; i++)
        {
            int j = i;
            btn[i].OnInteract += delegate ()
            {
                HandlePress(j);
                return false;
            };
        }
    }

    /// <summary>
    /// Generates the numbers of the buttons and the sorting algorhithm needed.
    /// </summary>
    private void Init()
    {
        if (_playSound)
        {
            Audio.PlaySoundAtTransform("bogosort", Module.transform);
            _playSound = false;
        }

        byte sorted = 0;

        //loop if the scramble happens to be already sorted
        do
        {
            sorted = 0;

            //generates new scramble
            for (int i = 0; i < 5; i++)
                GenerateNumber(i);

            //checks to see how many are sorted
            for (int i = 0; i < _buttons.Length - 1; i++)
            {
                if (_buttons[i] <= _buttons[i + 1])
                    sorted++;
            }
        } while (sorted == _buttons.Length - 1);

        //get random algorithm
        _currentAlgorithm = _algorithms[Random.Range(0, _algorithms.Length)];
        //_currentAlgorithm = "FIVE";
        Screen.text = _currentAlgorithm;

        Debug.LogFormat("[Sorting #{0}] Algorithm recieved: {1}", _moduleId, Screen.text);
        Debug.LogFormat("");

        //_initialButtons = new byte[5] { 1, 2, 3, 4, 5 };
    }

    /// <summary>
    /// Generates a new problem from scratch.
    /// </summary>
    /// <param name="num">The index of the buttons array.</param>
    private void GenerateNumber(int num)
    {
        //resets required swap count before breaking
        _swapIndex = 1;

        byte rng = (byte)Random.Range(0, 100);

        //get random numbers
        if (!_buttons.Contains(rng))
        {
            _initialButtons[num] = rng;
            _buttons[num] = rng;

            Debug.LogFormat("[Sorting #{0}] Button {1} has the label \"{2}\".", _moduleId, num + 1, rng);
        }

        //duplicate number prevention
        else
            GenerateNumber(num);

        ResetButtons();
    }

    /// <summary>
    /// Resets all numbers back to their original state, call this when striked.
    /// </summary>
    private void ResetNumber()
    {
        //resets required swap count before breaking
        _swapIndex = 1;

        //get initial buttons
        for (int i = 0; i < _initialButtons.Length; i++)
        {
            _buttons[i] = _initialButtons[i];
            txt[i].text = _initialButtons[i].ToString();

            while (txt[i].text.Length < 2)
                txt[i].text = txt[i].text.Insert(0, "0");
        }

        Screen.text = _currentAlgorithm;
    }

    /// <summary>
    /// Handles pressing of all buttons and screens (aside from submit)
    /// </summary>
    /// <param name="num">The index for the 5 buttons so the program can differentiate which button was pushed.</param>
    private void HandlePress(int num)
    {
        //plays button sound effect
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btn[num].transform);
        btn[num].AddInteractionPunch();
        Audio.PlaySoundAtTransform("tick", Module.transform);

        //if lights are off, the buttons should do nothing
        if (!_lightsOn || _isSolved || _buttonDelay)
        {
            string playSound = "button" + (num + 1).ToString();
            Audio.PlaySoundAtTransform(playSound, Module.transform);
            return;
        }

        //selecting a button
        if (!_selected.Contains((byte)num))
        {
            Audio.PlaySoundAtTransform("select", Module.transform);
            _selected.Add((byte)num);
            btn[num].GetComponent<MeshRenderer>().material.color = _highlightColor;
            _pushTimes++;
        }

        //unselecting a button
        else
        {
            Audio.PlaySoundAtTransform("deselect", Module.transform);
            _selected.Remove((byte)num);
            btn[num].GetComponent<MeshRenderer>().material.color = _buttonColor;
        }

        //if you selected 2 buttons
        if (_selected.Count == 2)
        {
            //block inputs from user temporarily
            _buttonDelay = true;

            Audio.PlaySoundAtTransform("swap", Module.transform);
            CheckSwap();
        }

        //bogosort easter egg activation
        if (_pushTimes == 55)
        {
            Audio.PlaySoundAtTransform("bogosort", Module.transform);
            _pushTimes = 0;

            //regenerates the numbers to prevent memorization
            for (int i = 0; i < btn.Length; i++)
            {
                GenerateNumber(i);
            }

            //bogosort
            if (!_bogoSort)
            {
                Debug.LogFormat("[Sorting #{0}] BogoSort activated!", _moduleId);
                Debug.LogFormat("[Sorting #{0}] All logs from this module are now disabled to prevent spam during BogoSort.", _moduleId);
                Debug.LogFormat("");

                _bogoSort = true;

                //set screen text to bogosort and run bogosort method
                Screen.text = "BOGO";

                for (int i = 0; i < _buttons.Length; i++)
                {
                    byte rng = (byte)Random.Range(10, 100);

                    //get random numbers
                    if (!_buttons.Contains(rng))
                    {
                        _buttons[i] = rng;

                        Debug.LogFormat("[Sorting #{0}] Button {1} has the label \"{2}\".", _moduleId, i + 1, rng);
                    }

                    //duplicate number prevention
                    else
                        i--;
                }
            }

            else
            {
                Debug.LogFormat("[Sorting #{0}] BogoSort deactivated!", _moduleId);
                Debug.LogFormat("[Sorting #{0}] All logs from this module are now reenabled.", _moduleId);
                Debug.LogFormat("");

                _bogoSort = false;

                ResetNumber();
            }
        }
    }

    /// <summary>
    /// Swaps the two buttons selected.
    /// </summary>
    private void CheckSwap()
    {
        //reset bogosort easter egg progress
        _pushTimes = 0;

        //information dump, bogosort should not state this information due to potential spam with the amount of swaps you have to make
        if (!_bogoSort)
            Debug.LogFormat("[Sorting #{0}] Swapping buttons {1} to {2}", _moduleId, _buttons[_selected[0]], _buttons[_selected[1]]);

        //ensures that the highest number is the least significant digit
        if (_selected[0] < _selected[1])
        {
            //lower number translates to most significant digit
            _swapButtons = (byte)((_selected[0] + 1) * 10);
            _swapButtons += (byte)(_selected[1] + 1);
        }

        else
        {
            //lower number translates to most significant digit
            _swapButtons = (byte)((_selected[1] + 1) * 10);
            _swapButtons += (byte)(_selected[0] + 1);
        }

        //checks if the swap is valid
        if (algorithms.IfValid(Screen.text, _initialButtons, _swapButtons, _swapIndex, _moduleId, Info.GetSerialNumberNumbers))
            DoSwap();

        else
        {
            Debug.LogFormat("[Sorting #{0}] Swap was invalid! Strike! The buttons have been reorganized back into their original state.", _moduleId);
            Audio.PlaySoundAtTransform("moduleStrike", Module.transform);
            Module.HandleStrike();

            background.GetComponent<MeshRenderer>().material.color = _strikeBackgroundColor;

            //resets the buttons
            for (int i = 0; i < btn.Length; i++)
            {
                btn[i].GetComponent<MeshRenderer>().material.color = _strikeColor;
            }

            //reset buttons so that they are ready to be pressed again
            ResetNumber();
            Invoke("ResetButtons", 0.2f);
        }
    }

    private void DoSwap()
    {
        if (!_bogoSort)
            Audio.PlaySoundAtTransform("swapSuccess", Module.transform);

        _buttonDelay = true;
        _swapIndex++;
        //Debug.LogFormat("Swap index is now {0}", _swapIndex);

        //gets the positions of both buttons
        for (int i = 0; i < pos.Length; i++)
        {
            pos[i].position = btn[_selected[i]].transform.position;
        }

        //the update function will animate this for however many frames this is set to
        _swapAnimated = 1;

        //swapping labels
        byte temp = _buttons[_selected[0]];
        _buttons[_selected[0]] = _buttons[_selected[1]];
        _buttons[_selected[1]] = temp;

        string debugList = "";

        //get current information about buttons
        for (int i = 0; i < _buttons.Length; i++)
        {
            debugList += _buttons[i].ToString() + " ";
        }

        //information dump, bogosort should not state this information due to potential spam with the amount of swaps you have to make
        if (!_bogoSort)
            Debug.LogFormat("[Sorting #{0}] Swap was valid! Both buttons have switched positions. Current position: {1}", _moduleId, debugList);

        //check if module has been solved
        CheckSolved();
    }

    /// <summary>
    /// Reset the button colors.
    /// </summary>
    private void ResetButtons()
    {
        //clear lists and allow button registries
        _buttonDelay = false;

        background.GetComponent<MeshRenderer>().material.color = _backgroundColor;

        //resets the buttons
        for (int i = 0; i < btn.Length; i++)
        {
            btn[i].GetComponent<MeshRenderer>().material.color = _buttonColor;

            //if it's bogosort, it should remain as ??
            if (!_bogoSort)
            {
                txt[i].text = _buttons[i].ToString();

                while (txt[i].text.Length < 2)
                    txt[i].text = txt[i].text.Insert(0, "0");
            }
        }

        //resets selected
        _selected = new List<byte>();
    }

    private void CheckSolved()
    {
        byte sorted = 0;

        //checks to see how many are sorted
        for (int i = 0; i < _buttons.Length - 1; i++)
        {
            if (_buttons[i] <= _buttons[i + 1])
                sorted++;
        }

        //information dump, bogosort should not state this information due to potential spam with the amount of swaps you have to make
        if (!_bogoSort)
        {
            Debug.LogFormat("[Sorting #{0}] {1}/{2} buttons are now sorted.", _moduleId, sorted + 1, _buttons.Length);
            Debug.LogFormat("");
        }

        //checks if everything is sorted
        if (sorted == _buttons.Length - 1)
        {
            _isSolved = true;
            Audio.PlaySoundAtTransform("modulePass", Module.transform);

            Debug.LogFormat("[Sorting #{0}] All buttons sorted, module solved!", _moduleId);
            Debug.LogFormat("");
            Module.HandlePass();
        }
    }

    /// <summary>
    /// Determines whether the input from the TwitchPlays chat command is valid or not.
    /// </summary>
    /// <param name="par">The string from the user.</param>
    private bool IsValid(string par)
    {
        string[] validNumbers = { "1", "2", "3", "4", "5" };

        if (validNumbers.Contains(par))
            return true;

        return false;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} swap <#> <#> (Swaps the labels in position '#' | valid numbers are from 1-5 | example: !swap 2 4";
#pragma warning restore 414

    /// <summary>
    /// TwitchPlays Compatibility, detects every chat message and clicks buttons accordingly.
    /// </summary>
    /// <param name="command">The twitch command made by the user.</param>
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] buttonSwapped = command.Split(' ');

        //if command is formatted correctly
        if (Regex.IsMatch(buttonSwapped[0], @"^\s*swap\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;

            //if command has no parameters
            if (buttonSwapped.Length < 3)
                yield return "sendtochaterror Please specify the 2 labels you want to swap! (Valid: 1-5)";

            //if command has too many parameters
            else if (buttonSwapped.Length > 3)
                yield return "sendtochaterror Too many buttons swapped! Only two labels can be swapped at a time.";

            //if command has an invalid parameter
            else if (!IsValid(buttonSwapped.ElementAt(1)) && !IsValid(buttonSwapped.ElementAt(2)))
                yield return "sendtochaterror Invalid number! Only label positions 1-5 can be swapped.";

            //if command is valid, push button accordingly
            else
            {
                byte seq1 = 0, seq2;

                byte.TryParse(buttonSwapped[1], out seq1);
                byte.TryParse(buttonSwapped[2], out seq2);

                btn[seq1 - 1].OnInteract();

                yield return new WaitForSeconds(0.25f);

                btn[seq2 - 1].OnInteract();
            }
        }
    }

    /// <summary>
    /// Force the module to be solved in TwitchPlays
    /// </summary>
    IEnumerator TwitchHandleForcedSolve()
    {
        Module.HandlePass();
        _isSolved = true;
        yield return null;
    }
}
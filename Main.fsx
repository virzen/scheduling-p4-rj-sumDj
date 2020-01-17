#load "Utils.fsx"

open System
open Utils

type TaskProperties =
    { p: int
      r: int
      d: int }

type Task =
    { id: int
      properties: TaskProperties }

type Instance = seq<Task>

type Solution =
  seq<seq<Task>>

module Task =
    let toString (task: Task): string =
        [ task.properties.p; task.properties.r; task.properties.d ]
          |> List.map string
          |> joinWith " "

    let id task =
        task.id

    let readyTime task =
        task.properties.r


module TaskProperties =

    let generateRandom (random: Random) =
        { p = (random.Next(1, 10))
          r = (random.Next(1, 10))
          d = (random.Next(1, 10)) }

    let create (p: int) (r: int) (d: int): TaskProperties =
        assert2 (p >= 1) "task must have non-zero length" |> ignore
        assert2 (p <= (d - r)) "task must doable within ready time and due date" |> ignore
        assert2 (d > r) "ready time must be before due date" |> ignore

        { p = p; r = r; d = d }

    let deserialize (s: string): TaskProperties =
      let numbers = splitOn " " s |> Array.map intOfString

      create numbers.[0] numbers.[1] numbers.[2]

    let toPrettyString task = String.replicate task.r " " + String.replicate task.p "X"

    let printMany (tasks: seq<TaskProperties>) =
        tasks
        |> Seq.map toPrettyString
        |> joinWith "\n"
        |> printf "\n%s\n"





module Instance =
    let generateFullyRandomly random size =
        seq { 1 .. size }
        |> Seq.map (fun n -> { id = n; properties = TaskProperties.generateRandom random })

    module Group =
        type GenerationMethod =
            | EDD
            | Longest

        let methods = [| EDD; Longest |]

        let generateNumbersSummingTo (random: Random) sum =
            assert2 (sum >= 2) "sum must be at least 2"

            let rand = random.Next(1, sum - 1)
            let a = rand
            let b = sum - rand

            (a, b)

        let generateForEDD (random: Random) r =
            let generateTriple _ =
                let pi = random.Next(2, 10)
                let (pi1, pi2) = (generateNumbersSummingTo random pi)
                let d = r + pi

                [ (TaskProperties.create pi r d)
                  (TaskProperties.create pi1 r d)
                  (TaskProperties.create pi2 r d ) ]

            seq { 1 .. 2 }
            |> Seq.map generateTriple
            |> List.concat
            |> List.toSeq



        let generateForLongest (random: Random) r =
            let generatePair _ =
                let pi = random.Next(3, 10)
                let pi1 = random.Next(1, pi - 1)
                let a = (r + pi + pi1)
                let di = random.Next(a, a + 10)
                let di1 = random.Next(r + pi1, a)

                [ (TaskProperties.create pi r di)
                  (TaskProperties.create pi1 r di1) ]

            seq { 1 .. 3 }
            |> Seq.map generatePair
            |> List.concat
            |> List.toSeq

        let create (random: Random) rRange method count =
            let r = random.Next(1, rRange)

            let group =
                match method with
                | EDD -> generateForEDD random r
                | Longest -> generateForLongest random r

            Seq.take count group


    let fromFile (filename: string): Instance =
      let lines = readLines filename |> Array.filter isNotEmpty
      let totalTasks = lines.[0] |> intOfString
      let tasksProps = Array.map TaskProperties.deserialize lines.[1..]

      assert2 (totalTasks = Array.length tasksProps) |> ignore

      let tasks = Array.mapi (fun index props -> { id = index + 1; properties = props }) tasksProps

      Seq.ofArray tasks

    // TODO: condense
    let generate instanceSize: Instance =
        let random = Random()
        let generateGroupsWithMethod (method, groupCounts) =  Seq.map (Group.create random instanceSize method) groupCounts

        let groupSize = 6

        let groupCounts =
            seq { 1 .. instanceSize }
            |> Seq.chunkBySize groupSize
            |> Seq.map (Array.length)

        let groupCountsChunkSize =
            [ instanceSize
              (Array.length Group.methods)
              6 ]
            |> List.map float
            |> List.reduce (fun a b -> ceil (a / b))
            |> int

        groupCounts
        |> Seq.chunkBySize groupCountsChunkSize
        |> Seq.zip Group.methods
        |> Seq.map generateGroupsWithMethod
        |> Seq.concat
        |> Seq.concat
        |> Seq.mapi (fun index taskProps -> { id = index + 1; properties = taskProps })

    let serialize (i: Instance) =
        let sizeString = string (Seq.length i)

        let taskStrings =
            i
            |> Seq.map Task.toString
            |> List.ofSeq

        sizeString :: taskStrings
        |> joinWith "\n"


type Machine =
  { id: int
    tasks: Task list }

module Machine =
  let addTask t m =
    let newTasks = List.append m.tasks [t]

    { m with tasks = newTasks }

  let empty (id: int): Machine =
    { id = id; tasks = [] }

  let lastEnd (machine: Machine): int =
    List.fold (fun last task -> (max last task.properties.r) + task.properties.p) 0 machine.tasks

  let id (m: Machine) =
    m.id

  let tasks (m: Machine) =
    m.tasks





module Solution =
    let solveRandom (instance: Instance): Solution =
        let random = Random()

        Seq.groupBy (fun _ -> random.Next(1, 5)) instance
        |> Seq.map (fun (_, tasks) -> tasks)

    let solveStatic (instance: Instance): Solution =
        let sizePerMachine = ((Seq.length instance) ./. 4) |> ceil |> int

        Seq.chunkBySize sizePerMachine instance
        |> Seq.map Seq.ofArray

    let solveReference (instance: Instance): Solution =
      chunkInto 4 instance

    let ofMachineList (machines: Machine list): Solution =
       machines
       |> List.sortBy Machine.id
       |> List.map Machine.tasks
       |> nestedListsToSeqs


    let solveFirstFreeMachineByReadyTime (instance: Instance): Solution =
      let sortedTasks = Seq.sortBy Task.readyTime instance |> List.ofSeq

      let firstReadyMachine machines = List.minBy Machine.lastEnd machines

      let rec step machines tasksLeft =
        match tasksLeft with
        | [] -> machines
        | task::rest ->
            let selectedMachine = firstReadyMachine machines
            let withTask = Machine.addTask task selectedMachine
            let newMachines = replaceInList selectedMachine withTask machines

            step newMachines rest

      let initialMachines = [(Machine.empty 1); (Machine.empty 2); (Machine.empty 3); (Machine.empty 4)]

      step initialMachines sortedTasks |> ofMachineList



    type Accumulator =
      { lastEnd: int; lateness: int }

    let latenessPerMachine (tasks: seq<Task>) =
        let accumulate acc task =
          let startTime = max acc.lastEnd task.properties.r
          let endTime = startTime + task.properties.p
          let lateness = max 0 (endTime - task.properties.d)

          { lastEnd = endTime; lateness = acc.lateness + lateness }

        let result = Seq.fold accumulate { lastEnd = 0; lateness = 0 } tasks

        result.lateness

    let totalLateness (s: Solution) =
        Seq.map latenessPerMachine s |> Seq.sum

    let serialize (s: Solution): string =
        let machineToString (tasks: seq<Task>): string =
          Seq.map Task.id tasks |> Seq.map string |> joinWith " "

        let latenessString = totalLateness s |> string
        let machinesStrings = Seq.map machineToString s |> joinWith "\n"

        [ latenessString; machinesStrings ] |> joinWith "\n"


// MAIN
let indexNumber = 133865

let generateInstanceAndSolution n =
  let instance = Instance.generate n
  let solution: Solution = Solution.solveReference instance

  let inFilename = "in" + (string indexNumber) + "_" + (string n) + ".txt"
  let outFilename = "out" + (string indexNumber) + "_" + (string n) + ".txt"

  instance |> Instance.serialize |> writeToFile inFilename
  solution |> Solution.serialize |> writeToFile outFilename

let fileName (path: string): string =
  IO.Path.GetFileName path

let directoryName (path: string): string =
  IO.Path.GetDirectoryName path


let instanceFilenameToSortable (path: string) =
  let name = fileName path
  let withoutExtension = splitOn "." name |> nth 0
  let elements = splitOn "_" withoutExtension
  let paddedSize = padLeft '0' 3 elements.[1]

  sprintf "%s%s" elements.[0] paddedSize


let studentIdFromPath path =
  let name = fileName path
  let withoutExtension = splitOn "." name |> nth 0
  let basis = splitOn "_" withoutExtension

  basis.[0]

let filenameInToOut (s: string): string =
  replaceInString "in" "out" s

let isInputFile (path: string): bool =
  (fileName path).StartsWith "in"


let solveAllInDirectory solver dirName =
  let filenames =
    filesInDirectory dirName
    |> Array.filter isInputFile
    |> Array.sortBy instanceFilenameToSortable
  let instances = Array.map Instance.fromFile filenames
  let solutions = Array.map solver instances
  let latenesses = Array.map Solution.totalLateness solutions

  let solutionsPerStudent (id, pairs) =
    let latenesses = Array.map (fun (_filename, lateness) -> lateness) pairs |> Array.map string |> List.ofArray

    id::latenesses |> joinWith "\n"

  Array.zip filenames latenesses
  |> Array.groupBy (fun (a, _b) -> studentIdFromPath a)
  |> Array.map solutionsPerStudent
  |> joinWith "\n\n"
  |> printf "%s"


let measureSolving (path: string) =
  let name = fileName path
  let directory = directoryName path
  let outFileName = filenameInToOut name

  let instance = Instance.fromFile path

  let stopWatch = Diagnostics.Stopwatch.StartNew()
  let solution = Solution.solveFirstFreeMachineByReadyTime instance
  stopWatch.Stop()

  solution
  |> Solution.serialize
  |> writeToFile (directory + "/rozwiazania/" + outFileName)

  stopWatch.Elapsed.TotalMilliseconds


let measureSolvingNTimes times path =
  let measurements = seq { 0 .. times } |> Seq.map (fun _ -> measureSolving path)
  let sum = Seq.sum measurements
  let mean = sum / (float (Seq.length measurements))

  mean

let f path =
  let meanMeasurement = measureSolvingNTimes 30 path

  path, meanMeasurement

let printResults (studentId, pairs) =
  let resultsString =
    pairs
    |> Seq.map (fun (_path, result) -> result)
    |> Seq.map string
    |> joinWith "\n"

  printfn "%s\n%s\n\n" studentId resultsString


let generateMeasurementsForAllInDirectory dir =
  filesInDirectory dir
  |> Array.filter isInputFile
  |> Array.sortBy instanceFilenameToSortable
  |> Array.map f
  |> Array.groupBy (fun (path, _result) -> studentIdFromPath path)
  |> Array.iter printResults

solveAllInDirectory Solution.solveReference "wszystkie-wrzucone"

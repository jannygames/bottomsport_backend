-- phpMyAdmin SQL Dump
-- version 5.2.1
-- https://www.phpmyadmin.net/
--
-- Host: 127.0.0.1
-- Generation Time: Apr 24, 2025 at 10:09 PM
-- Server version: 10.4.32-MariaDB
-- PHP Version: 8.2.12

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Database: `bottomsport`
--
CREATE DATABASE IF NOT EXISTS `bottomsport` DEFAULT CHARACTER SET utf8 COLLATE utf8_lithuanian_ci;
USE `bottomsport`;

-- --------------------------------------------------------

--
-- Table structure for table `actions`
--

DROP TABLE IF EXISTS `actions`;
CREATE TABLE `actions` (
  `id` int(11) NOT NULL,
  `action_type` enum('hit','stand','double','split','dealer') DEFAULT NULL,
  `time` datetime DEFAULT NULL,
  `rank` varchar(10) DEFAULT NULL,
  `symbol` varchar(10) DEFAULT NULL,
  `is_hidden` tinyint(1) DEFAULT NULL,
  `user_id` int(11) DEFAULT NULL,
  `game_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `actions`:
--   `user_id`
--       `users` -> `id`
--   `game_id`
--       `games` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `bets`
--

DROP TABLE IF EXISTS `bets`;
CREATE TABLE `bets` (
  `id` int(11) NOT NULL,
  `sum` float DEFAULT NULL,
  `bet_time` datetime DEFAULT NULL,
  `user_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `bets`:
--   `user_id`
--       `users` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `bots`
--

DROP TABLE IF EXISTS `bots`;
CREATE TABLE `bots` (
  `id` int(11) NOT NULL,
  `strategy` enum('aggressive','conservative') DEFAULT NULL,
  `betting_sum` float DEFAULT NULL,
  `user_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `bots`:
--   `user_id`
--       `users` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `games`
--

DROP TABLE IF EXISTS `games`;
CREATE TABLE `games` (
  `id` int(11) NOT NULL,
  `start_time` datetime DEFAULT NULL,
  `end_time` datetime DEFAULT NULL,
  `card_placement_hash` varchar(255) DEFAULT NULL,
  `room_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `games`:
--   `room_id`
--       `rooms` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `mcstats`
--

DROP TABLE IF EXISTS `mcstats`;
CREATE TABLE `mcstats` (
  `id` int(11) NOT NULL,
  `game_num` int(11) DEFAULT NULL,
  `game_date` date DEFAULT NULL,
  `steps_done` int(11) DEFAULT NULL,
  `bet_amount` float DEFAULT NULL,
  `winnings` float DEFAULT NULL,
  `result` enum('win','loss') DEFAULT NULL,
  `mission_game_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `mcstats`:
--   `mission_game_id`
--       `missioncrossablegames` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `missioncrossablegames`
--

DROP TABLE IF EXISTS `missioncrossablegames`;
CREATE TABLE `missioncrossablegames` (
  `id` int(11) NOT NULL,
  `difficulty` enum('easy','medium','hard','daredevil') DEFAULT NULL,
  `bet_amount` float DEFAULT NULL,
  `prize_multiplier` float DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `missioncrossablegames`:
--

-- --------------------------------------------------------

--
-- Table structure for table `roomparticipants`
--

DROP TABLE IF EXISTS `roomparticipants`;
CREATE TABLE `roomparticipants` (
  `id` int(11) NOT NULL,
  `user_id` int(11) DEFAULT NULL,
  `room_id` int(11) DEFAULT NULL,
  `is_playing` tinyint(1) DEFAULT NULL,
  `is_creator` tinyint(1) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `roomparticipants`:
--   `user_id`
--       `users` -> `id`
--   `room_id`
--       `rooms` -> `id`
--

--
-- Dumping data for table `roomparticipants`
--

INSERT INTO `roomparticipants` (`id`, `user_id`, `room_id`, `is_playing`, `is_creator`) VALUES
(5, 7, 6, 0, 1),
(6, 7, 7, 0, 1),
(7, 7, 8, 0, 1),
(8, 7, 9, 0, 1);

-- --------------------------------------------------------

--
-- Table structure for table `rooms`
--

DROP TABLE IF EXISTS `rooms`;
CREATE TABLE `rooms` (
  `id` int(11) NOT NULL,
  `title` varchar(100) DEFAULT NULL,
  `room_creator` int(11) DEFAULT NULL,
  `min_bet` float DEFAULT NULL,
  `max_bet` float DEFAULT NULL,
  `room_status` enum('active','inactive','hidden') DEFAULT NULL,
  `creation_date` date DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `rooms`:
--   `room_creator`
--       `users` -> `id`
--

--
-- Dumping data for table `rooms`
--

INSERT INTO `rooms` (`id`, `title`, `room_creator`, `min_bet`, `max_bet`, `room_status`, `creation_date`) VALUES
(6, 'AAA', 7, 10, 1000, 'active', '2025-04-24'),
(7, 'aaaaa', 7, 10, 1000, 'active', '2025-04-24'),
(8, 'Hello Bitches', 7, 22, 2222, 'active', '2025-04-24'),
(9, 'HAHAHAHAHA IT WORKS', 7, 1000, 100000, 'active', '2025-04-24');

-- --------------------------------------------------------

--
-- Table structure for table `suspicions`
--

DROP TABLE IF EXISTS `suspicions`;
CREATE TABLE `suspicions` (
  `id` int(11) NOT NULL,
  `user_id` int(11) DEFAULT NULL,
  `date` date DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `suspicions`:
--   `user_id`
--       `users` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `tournaments`
--

DROP TABLE IF EXISTS `tournaments`;
CREATE TABLE `tournaments` (
  `id` int(11) NOT NULL,
  `title` varchar(100) DEFAULT NULL,
  `password_hash` varchar(255) DEFAULT NULL,
  `participation_sum` float DEFAULT NULL,
  `max_players` int(11) DEFAULT NULL,
  `min_players` int(11) DEFAULT NULL,
  `games_count` int(11) DEFAULT NULL,
  `tournament_status` enum('active','inactive','finished') DEFAULT NULL,
  `room_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `tournaments`:
--   `room_id`
--       `rooms` -> `id`
--

-- --------------------------------------------------------

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
CREATE TABLE `users` (
  `id` int(11) NOT NULL,
  `username` varchar(50) NOT NULL,
  `password_hash` varchar(255) NOT NULL,
  `role` enum('user','admin','player') NOT NULL,
  `balance` float DEFAULT NULL,
  `registration_date` date DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_lithuanian_ci;

--
-- RELATIONSHIPS FOR TABLE `users`:
--

--
-- Dumping data for table `users`
--

INSERT INTO `users` (`id`, `username`, `password_hash`, `role`, `balance`, `registration_date`) VALUES
(1, 'jannygamesstudio', '73l8gRjwLftklgfdXT+MdiMEjJwGPVMsyVxe16iYpk8=', 'user', 0, '2025-04-13'),
(2, 'jamolisdev', '73l8gRjwLftklgfdXT+MdiMEjJwGPVMsyVxe16iYpk8=', 'user', 0, '2025-04-13'),
(3, 'test', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', 'user', 0, '2025-04-13'),
(4, 'testuser', 'E9JJ8stBJ7QM+nV4ZoUCeHk/gU3tPFh/5YieiJp6n2w=', 'user', 0, '2025-04-13'),
(5, 'testuser2', 'n2Vnptii6uYaETmxk8mB3W/MI5nwd2DeyN49Rp3s9ao=', 'user', 0, '2025-04-13'),
(6, 'testas', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', 'user', 0, '2025-04-15'),
(7, 'test1', '123456', 'user', 0, '2025-04-15'),
(8, 'daniel', '123', 'user', 0, '2025-04-24');

--
-- Indexes for dumped tables
--

--
-- Indexes for table `actions`
--
ALTER TABLE `actions`
  ADD PRIMARY KEY (`id`),
  ADD KEY `user_id` (`user_id`),
  ADD KEY `game_id` (`game_id`);

--
-- Indexes for table `bets`
--
ALTER TABLE `bets`
  ADD PRIMARY KEY (`id`),
  ADD KEY `user_id` (`user_id`);

--
-- Indexes for table `bots`
--
ALTER TABLE `bots`
  ADD PRIMARY KEY (`id`),
  ADD KEY `user_id` (`user_id`);

--
-- Indexes for table `games`
--
ALTER TABLE `games`
  ADD PRIMARY KEY (`id`),
  ADD KEY `room_id` (`room_id`);

--
-- Indexes for table `mcstats`
--
ALTER TABLE `mcstats`
  ADD PRIMARY KEY (`id`),
  ADD KEY `mission_game_id` (`mission_game_id`);

--
-- Indexes for table `missioncrossablegames`
--
ALTER TABLE `missioncrossablegames`
  ADD PRIMARY KEY (`id`);

--
-- Indexes for table `roomparticipants`
--
ALTER TABLE `roomparticipants`
  ADD PRIMARY KEY (`id`),
  ADD KEY `user_id` (`user_id`),
  ADD KEY `room_id` (`room_id`);

--
-- Indexes for table `rooms`
--
ALTER TABLE `rooms`
  ADD PRIMARY KEY (`id`),
  ADD KEY `room_creator` (`room_creator`);

--
-- Indexes for table `suspicions`
--
ALTER TABLE `suspicions`
  ADD PRIMARY KEY (`id`),
  ADD KEY `user_id` (`user_id`);

--
-- Indexes for table `tournaments`
--
ALTER TABLE `tournaments`
  ADD PRIMARY KEY (`id`),
  ADD KEY `room_id` (`room_id`);

--
-- Indexes for table `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`);

--
-- AUTO_INCREMENT for dumped tables
--

--
-- AUTO_INCREMENT for table `actions`
--
ALTER TABLE `actions`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `bets`
--
ALTER TABLE `bets`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `bots`
--
ALTER TABLE `bots`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `games`
--
ALTER TABLE `games`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `mcstats`
--
ALTER TABLE `mcstats`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `missioncrossablegames`
--
ALTER TABLE `missioncrossablegames`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `roomparticipants`
--
ALTER TABLE `roomparticipants`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=9;

--
-- AUTO_INCREMENT for table `rooms`
--
ALTER TABLE `rooms`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=10;

--
-- AUTO_INCREMENT for table `suspicions`
--
ALTER TABLE `suspicions`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `tournaments`
--
ALTER TABLE `tournaments`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT;

--
-- AUTO_INCREMENT for table `users`
--
ALTER TABLE `users`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=9;

--
-- Constraints for dumped tables
--

--
-- Constraints for table `actions`
--
ALTER TABLE `actions`
  ADD CONSTRAINT `actions_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `actions_ibfk_2` FOREIGN KEY (`game_id`) REFERENCES `games` (`id`);

--
-- Constraints for table `bets`
--
ALTER TABLE `bets`
  ADD CONSTRAINT `bets_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`);

--
-- Constraints for table `bots`
--
ALTER TABLE `bots`
  ADD CONSTRAINT `bots_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`);

--
-- Constraints for table `games`
--
ALTER TABLE `games`
  ADD CONSTRAINT `games_ibfk_1` FOREIGN KEY (`room_id`) REFERENCES `rooms` (`id`);

--
-- Constraints for table `mcstats`
--
ALTER TABLE `mcstats`
  ADD CONSTRAINT `mcstats_ibfk_1` FOREIGN KEY (`mission_game_id`) REFERENCES `missioncrossablegames` (`id`);

--
-- Constraints for table `roomparticipants`
--
ALTER TABLE `roomparticipants`
  ADD CONSTRAINT `roomparticipants_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`),
  ADD CONSTRAINT `roomparticipants_ibfk_2` FOREIGN KEY (`room_id`) REFERENCES `rooms` (`id`);

--
-- Constraints for table `rooms`
--
ALTER TABLE `rooms`
  ADD CONSTRAINT `rooms_ibfk_1` FOREIGN KEY (`room_creator`) REFERENCES `users` (`id`);

--
-- Constraints for table `suspicions`
--
ALTER TABLE `suspicions`
  ADD CONSTRAINT `suspicions_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`);

--
-- Constraints for table `tournaments`
--
ALTER TABLE `tournaments`
  ADD CONSTRAINT `tournaments_ibfk_1` FOREIGN KEY (`room_id`) REFERENCES `rooms` (`id`);
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
